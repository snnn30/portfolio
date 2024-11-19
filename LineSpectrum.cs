using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LineSpectrum : MonoBehaviour {
    [Header("��{�̉~")]
    [SerializeField] int _samplingLevel = 10;
    [SerializeField] float _radius = 0.5f;
    [SerializeField] float _heightMultiplier = 5f;
    [SerializeField] float _animationTime = 0.04f;
    [SerializeField] float _sensitivity = 40f;
    [SerializeField] float _rotateSpeed = 20f;
    [SerializeField] float _lineWidth = 0.01f;
    [SerializeField, Tooltip("LineRenderer�̒��_���ɂȂ�B")]
    int MELBANDS = 128;

    [Header("�F�i���[�ɓ����F��ݒ肵�Ăˁ�j")]
    [SerializeField] Gradient _colorGradient;
    [SerializeField] float _gradientDuration = 16;

    [Header("�ǉ��̉~")]
    [SerializeField] int _addCircleCount = 30;
    [SerializeField] float _circleSpace = 0.2f;
    [SerializeField] float _delaySec = 0.02f;

    CenterCircle _centerCircle;
    class CenterCircle {
        public LineRenderer lineRenderer;
        public float[] targetHeights;
        public float[] currentHeights;
        public float[] velocity;
        public Vector3[] linePositions;
        public float[] angles;

        public CenterCircle(LineRenderer lineRenderer, int pointsCount)
        {
            this.lineRenderer = lineRenderer;
            targetHeights = new float[pointsCount];
            currentHeights = new float[pointsCount];
            velocity = new float[pointsCount];
            linePositions = new Vector3[pointsCount];
            angles = new float[pointsCount];
        }
    }

    LineRenderer[] _circles;

    List<Log> _logList = new List<Log>();
    class Log {
        public float[] height;
        public float deltaTime;
        public Color color;
        public float[] angle;
        public Log(float[] height, float deltaTime, Color color, float[] angle)
        {
            this.height = height;
            this.deltaTime = deltaTime;
            this.color = color;
            this.angle = angle;
        }
    }

    int _maxFrameRate;
    float _elapsedTime = 0;
    int _pointsCount;

    int SAMPLE_RATE;
    int FFTSIZE;

    void Awake()
    {
        _maxFrameRate = Mathf.CeilToInt(float.Parse(Screen.currentResolution.refreshRateRatio.ToString()));
        FFTSIZE = (int)Mathf.Pow(2, _samplingLevel);

        _pointsCount = MELBANDS;
        AudioClip clip = this.GetComponent<AudioSource>().clip;
        SAMPLE_RATE = clip.frequency;

        // ��{�̉~�̍쐬
        _centerCircle = new CenterCircle(this.AddComponent<LineRenderer>(), _pointsCount);
        InitializeLineRenderer(_centerCircle.lineRenderer);

        // �ǉ��̉~�̍쐬
        _circles = new LineRenderer[_addCircleCount];
        for (int i = 0; i < _addCircleCount; i++) {
            _circles[i] = new GameObject($"LineRenderer_{i}").AddComponent<LineRenderer>();
            _circles[i].transform.parent = transform;
            InitializeLineRenderer(_circles[i].GetComponent<LineRenderer>());
        }

        void InitializeLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.startWidth = _lineWidth;
            lineRenderer.endWidth = _lineWidth;
            lineRenderer.loop = true;
            lineRenderer.positionCount = _pointsCount;
        }
    }

    void Update()
    {
        UpdateCenterCircle();

        UpdateLog();

        UpdateAdditionalCircle();
    }

    /// <summary>
    /// ��{�̉~�̍X�V
    /// _centerCircle�̍X�V�Ǝ��ۂ̍��W�A�F�̍X�V
    /// </summary>
    void UpdateCenterCircle()
    {
        float[] spectrumData = new float[FFTSIZE];
        AudioListener.GetSpectrumData(spectrumData, 0, FFTWindow.Hamming);
        float[] melSpectrum = SpectrumToMelScale(spectrumData, SAMPLE_RATE, FFTSIZE);

        for (int i = 0; i < _pointsCount; i++) {
            float frequencyScale = Mathf.Lerp(1f, _sensitivity, (float)i / _pointsCount);
            _centerCircle.targetHeights[i] = melSpectrum[i] * frequencyScale * _heightMultiplier;

            float angle = i * (360f / _pointsCount) + _elapsedTime * _rotateSpeed;
            _centerCircle.angles[i] = angle;
            float x = _radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = _radius * Mathf.Sin(angle * Mathf.Deg2Rad);

            _centerCircle.currentHeights[i] = Mathf.SmoothDamp(_centerCircle.currentHeights[i], _centerCircle.targetHeights[i], ref _centerCircle.velocity[i], _animationTime);
            _centerCircle.linePositions[i] = new Vector3(x, _centerCircle.currentHeights[i], z);
        }

        _centerCircle.lineRenderer.SetPositions(_centerCircle.linePositions);

        _elapsedTime += Time.deltaTime;
        float t = (_elapsedTime % _gradientDuration) / _gradientDuration;
        _centerCircle.lineRenderer.material.color = _colorGradient.Evaluate(t);
    }

    /// <summary>
    /// _logList�̍X�V
    /// </summary>
    void UpdateLog()
    {
        float[] heights = new float[_pointsCount];
        Array.Copy(_centerCircle.currentHeights, heights, _pointsCount);

        Color color = _centerCircle.lineRenderer.material.color;

        var log = new Log(heights, Time.deltaTime, color, _centerCircle.angles);
        _logList.Add(log);

        if (_logList.Count > (_addCircleCount + 1) * _maxFrameRate) {
            _logList.RemoveAt(0);
        }
    }

    /// <summary>
    /// �ǉ��̉~�̍X�V
    /// _centerCircle��_logList������W�ƐF������A�X�V
    /// </summary>
    void UpdateAdditionalCircle()
    {
        // �ǉ��̉~�̍X�V
        for (int circleIndex = 0; circleIndex < _circles.Length; circleIndex++) {
            Vector3[] positions = new Vector3[_pointsCount];

            float totalDelaySec = _delaySec * (circleIndex + 1);
            int delayFrames = 0;
            float sec = 0;
            while (sec < totalDelaySec) {
                if (_logList.Count - 1 - delayFrames < 0) { break; }
                sec += _logList[_logList.Count - 1 - delayFrames].deltaTime;
                delayFrames++;
            }
            int index = _logList.Count - 1 - delayFrames;

            // �~��̏���
            for (int i = 0; i < _pointsCount; i++) {
                float angle = i * (360f / _pointsCount);
                if (index >= 0) {
                    angle = _logList[index].angle[i];
                }
                float x = (_radius + (circleIndex + 1) * _circleSpace) * Mathf.Cos(angle * Mathf.Deg2Rad);
                float z = (_radius + (circleIndex + 1) * _circleSpace) * Mathf.Sin(angle * Mathf.Deg2Rad);
                positions[i] = new Vector3(x, 0, z);

                if (index >= 0) {
                    positions[i].y = _logList[index].height[i];
                }
            }

            _circles[circleIndex].SetPositions(positions);

            if (index < 0) {
                _circles[circleIndex].material.color = _centerCircle.lineRenderer.material.color;
            } else {
                _circles[circleIndex].material.color = _logList[index].color;
            }
        }
    }

    /// <summary>
    /// spectrum�������X�y�N�g���ɕϊ�
    /// </summary>
    float[] SpectrumToMelScale(float[] spectrum, int sampleRate, int fftSize)
    {
        float[] melBins = new float[MELBANDS + 1];
        float[] melSpectrum = new float[MELBANDS];

        // �����o���h�̋��E���g�����v�Z
        for (int i = 0; i < MELBANDS + 1; i++) {
            float melFreq = MelFrequency(i, MELBANDS + 1, sampleRate);
            melBins[i] = melFreq;
        }

        // ���ꂪ�ϓ��ɕ�����Ă���K�v����
        // foreach (float m in melBins) { Debug.Log(m); }


        // �����o���h�ɍ��킹�ăX�y�N�g���f�[�^���W��
        for (int i = 0; i < MELBANDS; i++) {
            float sum = 0;
            float count = 0;
            float startFreq = MelToFrequency(melBins[i]);
            float endFreq = MelToFrequency(melBins[i + 1]);

            // Debug.Log(startFreq + " " + endFreq);
            for (int j = 0; j < spectrum.Length; j++) {
                float freq = (j + 1) * (SAMPLE_RATE / 2) / spectrum.Length;
                if (startFreq <= freq && freq < endFreq) {
                    sum += spectrum[j];
                    count++;
                }
            }

            melSpectrum[i] = sum != 0 ? sum / count : 0;
        }

        return melSpectrum;
    }

    /// <summary>
    /// �������g���ւ̎��g���ϊ�
    /// </summary>
    float MelFrequency(int bin, int numMelBins, int sampleRate)
    {
        float fMax = sampleRate / 2f; // �ő���g��
        float melMax = 2595 * Mathf.Log10(1 + (fMax / 700));

        // �����ړx�̎��g�����v�Z
        if (bin == 0) {
            return 0;
        } else {
            return melMax / (float)(numMelBins - 1) * bin;
        }
    }

    /// <summary>
    /// �������g����ʏ�̎��g���ɖ߂�
    /// </summary>
    float MelToFrequency(float mel)
    {
        return 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
    }
}