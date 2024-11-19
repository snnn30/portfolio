using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LineSpectrum : MonoBehaviour {
    [Header("基本の円")]
    [SerializeField] int _samplingLevel = 10;
    [SerializeField] float _radius = 0.5f;
    [SerializeField] float _heightMultiplier = 5f;
    [SerializeField] float _animationTime = 0.04f;
    [SerializeField] float _sensitivity = 40f;
    [SerializeField] float _rotateSpeed = 20f;
    [SerializeField] float _lineWidth = 0.01f;
    [SerializeField, Tooltip("LineRendererの頂点数になる。")]
    int MELBANDS = 128;

    [Header("色（両端に同じ色を設定してね♪）")]
    [SerializeField] Gradient _colorGradient;
    [SerializeField] float _gradientDuration = 16;

    [Header("追加の円")]
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

        // 基本の円の作成
        _centerCircle = new CenterCircle(this.AddComponent<LineRenderer>(), _pointsCount);
        InitializeLineRenderer(_centerCircle.lineRenderer);

        // 追加の円の作成
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
    /// 基本の円の更新
    /// _centerCircleの更新と実際の座標、色の更新
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
    /// _logListの更新
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
    /// 追加の円の更新
    /// _centerCircleと_logListから座標と色を決定、更新
    /// </summary>
    void UpdateAdditionalCircle()
    {
        // 追加の円の更新
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

            // 円一つの処理
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
    /// spectrumをメルスペクトルに変換
    /// </summary>
    float[] SpectrumToMelScale(float[] spectrum, int sampleRate, int fftSize)
    {
        float[] melBins = new float[MELBANDS + 1];
        float[] melSpectrum = new float[MELBANDS];

        // メルバンドの境界周波数を計算
        for (int i = 0; i < MELBANDS + 1; i++) {
            float melFreq = MelFrequency(i, MELBANDS + 1, sampleRate);
            melBins[i] = melFreq;
        }

        // これが均等に分かれている必要あり
        // foreach (float m in melBins) { Debug.Log(m); }


        // メルバンドに合わせてスペクトルデータを集約
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
    /// メル周波数への周波数変換
    /// </summary>
    float MelFrequency(int bin, int numMelBins, int sampleRate)
    {
        float fMax = sampleRate / 2f; // 最大周波数
        float melMax = 2595 * Mathf.Log10(1 + (fMax / 700));

        // メル尺度の周波数を計算
        if (bin == 0) {
            return 0;
        } else {
            return melMax / (float)(numMelBins - 1) * bin;
        }
    }

    /// <summary>
    /// メル周波数を通常の周波数に戻す
    /// </summary>
    float MelToFrequency(float mel)
    {
        return 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
    }
}