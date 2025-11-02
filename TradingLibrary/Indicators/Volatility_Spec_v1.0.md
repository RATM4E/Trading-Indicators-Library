# SharedLibrary/Indicators/Volatility — Specification (v1.0)

**Goal:** deterministic, cross-platform-identical volatility indicators for **cTrader (C#)** and **MT5 (MQL5)**, consistent with `Core/` and `PriceAction` modules. Contract specification only (parameters, outputs, warm-up, policies).

---

## 0. Common Conventions

- **Indexing:** chronological (0 → oldest).  
- **Numeric type:** `double`.  
- **NaN policy:** incomplete data → `NaN`.  
- **Warm-up:** strictly equal to required lookback.  
- **Annualization:** when applicable, `σ_annual = σ_window * sqrt(tradingDaysPerYear / period)` (default `252`).  
- **Batch vs Stateful:** identical outputs within ±1e−12 tolerance.  
- **Dependencies:** `MathBase`, `PriceAction.TrueRange`, `MovingAverages`, `Statistics.StdDev`.

---

## 1) ATR Family

### 1.1 ATR (Average True Range)
**Definition:** smoothed average of `TrueRange`.  
**Parameters:**  
- `period=14`  
- `mode=AtrMode.RMA | EMA | SMA`

**Inputs:** `high[], low[], close[]`  
**Output:** `atr[]` (absolute value, not normalized)  
**Warm-up:** `period`  
**Stateful:** `class ATRState { double Update(h,l,c); }`

### 1.2 NATR (Normalized ATR)
**Definition:** `natr = ATR / Close` (×100 if scaled).  
**Parameters:**  
- `period=14`  
- `mode=AtrMode.RMA`  
- `scaleTo100=false`

**Outputs:** `natr[]`  
**Warm-up:** `period`

### 1.3 ATR Bands
**Definition:** `Basis ± Mult × ATR`.  
**Parameters:**  
- `basisPeriod=20`, `basisAvg=EMA`  
- `atrPeriod=14`, `atrMode=RMA`  
- `mult=2.0`  
**Inputs:** `close[], high[], low[]`  
**Outputs:** `(upper[], basis[], lower[])`  
**Warm-up:** `max(basisPeriod, atrPeriod)`

---

## 2) StdDev / ZScore / Historical Volatility

### 2.1 StdDev
**Definition:** rolling sample or population standard deviation.  
**Parameters:** `period=20`, `sample=true`  
**Inputs:** `src[]`  
**Output:** `std[]`  
**Warm-up:** `period`

### 2.2 ZScore
**Definition:** `(src − SMA(src,P)) / StdDev(src,P)`  
**Parameters:** `period=20`, `sample=true`  
**Outputs:** `z[]`  
**Warm-up:** `period`

### 2.3 Historical Volatility (HV)
**Definition:** standard deviation of log returns annualized.  
**Parameters:**  
- `period=20`  
- `annualize=true`  
- `tradingDaysPerYear=252`  
**Inputs:** `close[]`  
**Output:** `hv[]` (in decimal, not % unless scaled by UI)  
**Warm-up:** `period`

---

## 3) Bollinger Bands

**Definition:**  
- `Basis = MA(src, basisPeriod, basisAvg)`  
- `Upper = Basis + devMult × StdDev(src, basisPeriod)`  
- `Lower = Basis − devMult × StdDev(src, basisPeriod)`  
**Parameters:**  
- `basisPeriod=20`, `devMult=2.0`, `basisAvg=SMA`, `sampleStd=true`  
**Inputs:** `src[]`  
**Outputs:** `(upper[], basis[], lower[])`  
**Warm-up:** `basisPeriod`

### 3.1 Bollinger %B
`%B = (src − Lower) / (Upper − Lower)` ∈ [0,1]

### 3.2 Bollinger BandWidth
`BBW = (Upper − Lower) / Basis`

---

## 4) Keltner Channels

**Definition:**  
`Basis = MA(Close, basisPeriod, basisAvg)`  
`Upper = Basis + mult × Dev`  
`Lower = Basis − mult × Dev`  
где `Dev = ATR(period, atrMode)` или `EMA(TrueRange)`.

**Parameters:**  
- `basisPeriod=20`, `basisAvg=EMA`  
- `atrPeriod=10`, `atrMode=RMA`  
- `mult=2.0`  
- `devMode=AtrDevMode.ATR | TR_EMA`  
**Inputs:** `high[], low[], close[]`  
**Outputs:** `(upper[], basis[], lower[])`  
**Warm-up:** `max(basisPeriod, atrPeriod)`

### 4.1 Keltner BandWidth
`KBW = (Upper − Lower) / Basis`

---

## 5) Donchian Channel (Volatility Mode)

**Definition:**  
- `Upper = Highest(High, period)`  
- `Lower = Lowest(Low, period)`  
- `Mid = (Upper + Lower)/2`  
- `Width = Upper − Lower`  
- `PercentWidth = Width / Mid`

**Parameters:** `period=20`  
**Inputs:** `high[], low[]`  
**Outputs:** `(upper[], mid[], lower[])`, optional `width[]`, `percentWidth[]`  
**Warm-up:** `period`

---

## 6) Advanced Volatility Estimators

### 6.1 Parkinson Volatility
**Definition:** `σ = sqrt( (1/(4ln2)) * mean(ln(H/L)²) )`  
**Parameters:** `period=20`, `annualize=true`, `tradingDaysPerYear=252`  
**Inputs:** `high[], low[]`  
**Outputs:** `sigma[]` (decimal)  
**Warm-up:** `period`

### 6.2 Garman–Klass Volatility
**Definition:** `σ² = 0.5ln(H/L)² − (2ln2−1)ln(C/O)²`  
**Parameters:** as above.  
**Inputs:** `open[], high[], low[], close[]`  
**Outputs:** `sigma[]`  
**Warm-up:** `period`

### 6.3 Rogers–Satchell Volatility
**Definition:** `σ² = ln(H/C)*ln(H/O) + ln(L/C)*ln(L/O)`  
**Parameters:** as above.  
**Inputs:** `open[], high[], low[], close[]`  
**Outputs:** `sigma[]`  
**Warm-up:** `period`

### 6.4 Yang–Zhang Volatility
**Definition:** combines overnight gap + intraday components (Parkinson + RS + gap term).  
**Parameters:** `period=20`, `annualize=true`, `tradingDaysPerYear=252`  
**Inputs:** `open[], high[], low[], close[]`  
**Outputs:** `sigma[]`  
**Warm-up:** `period`

---

## 7) Chaikin Volatility (CV)

**Definition:** `%Δ EMA(H−L, emaPeriod)` over `rocPeriod` bars.  
`CV_t = 100 × (EMA(H−L,emaPeriod)_t − EMA(H−L,emaPeriod)_{t−roc}) / EMA(H−L,emaPeriod)_{t−roc}`  
**Parameters:**  
- `emaPeriod=10`, `rocPeriod=10`, `avg=EMA`, `scaleTo100=true`  
**Inputs:** `high[], low[]`  
**Outputs:** `cv[]` (decimal or %)  
**Warm-up:** `emaPeriod + rocPeriod`

---

## 8) Enumerations

```text
enum AtrMode { RMA=0, EMA=1, SMA=2 }
enum AvgMode { SMA, EMA, RMA, WMA }
enum AtrDevMode { ATR=0, TR_EMA=1 }    // for Keltner
```

---

## 9) C# and MQL5 Interfaces (no code)

### C# batch
```csharp
double[] ATR(double[] h, double[] l, double[] c, int period=14, AtrMode mode=AtrMode.RMA);
double[] NATR(double[] h, double[] l, double[] c, int period=14, AtrMode mode=AtrMode.RMA, bool scaleTo100=false);
(double[] u, double[] b, double[] d) ATRBands(double[] c, double[] h, double[] l, int basisP=20, int atrP=14, AtrMode atrMode=AtrMode.RMA, double mult=2.0, AvgMode basisAvg=AvgMode.EMA);
double[] StdDev(double[] src, int period=20, bool sample=true);
double[] ZScore(double[] src, int period=20, bool sample=true);
double[] HV(double[] c, int period=20, bool annualize=true, double tpy=252.0);
(double[] u, double[] b, double[] d) Bollinger(double[] src, int basisP=20, double mult=2.0, AvgMode avg=AvgMode.SMA, bool sampleStd=true);
double[] BollingerPercentB(double[] src, int basisP=20, double mult=2.0, AvgMode avg=AvgMode.SMA, bool sampleStd=true);
double[] BollingerBandwidth(double[] src, int basisP=20, double mult=2.0, AvgMode avg=AvgMode.SMA, bool sampleStd=true);
(double[] u, double[] b, double[] d) Keltner(double[] h, double[] l, double[] c, int basisP=20, AvgMode basisAvg=AvgMode.EMA, int atrP=10, AtrDevMode devMode=AtrDevMode.ATR, AtrMode atrMode=AtrMode.RMA, double mult=2.0);
(double[] u, double[] m, double[] d) DonchianChannel(double[] h, double[] l, int period=20);
double[] DonchianWidth(double[] h, double[] l, int period=20);
double[] DonchianPercentWidth(double[] h, double[] l, int period=20);
double[] ParkinsonVol(double[] h, double[] l, int period=20, bool annualize=true, double tpy=252.0);
double[] GarmanKlassVol(double[] o, double[] h, double[] l, double[] c, int period=20, bool annualize=true, double tpy=252.0);
double[] RogersSatchellVol(double[] o, double[] h, double[] l, double[] c, int period=20, bool annualize=true, double tpy=252.0);
double[] YangZhangVol(double[] o, double[] h, double[] l, double[] c, int period=20, bool annualize=true, double tpy=252.0);
double[] ChaikinVol(double[] h, double[] l, int emaPeriod=10, int rocPeriod=10, AvgMode avg=AvgMode.EMA, bool scaleTo100=true);
```

### MQL5 batch
```mqh
void ATR(const double &h[], const double &l[], const double &c[], const int period, const int mode, double &atr[]);
void NATR(const double &h[], const double &l[], const double &c[], const int period, const int mode, const bool scaleTo100, double &natr[]);
void ATRBands(const double &c[], const double &h[], const double &l[], const int basisP, const int atrP, const int atrMode, const double mult, const int basisAvg, double &u[], double &b[], double &d[]);
void StdDev(const double &src[], const int period, const bool sample, double &out[]);
void ZScore(const double &src[], const int period, const bool sample, double &out[]);
void HV(const double &c[], const int period, const bool annualize, const double tpy, double &out[]);
void Bollinger(const double &src[], const int basisP, const double mult, const int avg, const bool sampleStd, double &u[], double &b[], double &d[]);
void BollingerPercentB(const double &src[], const int basisP, const double mult, const int avg, const bool sampleStd, double &out[]);
void BollingerBandwidth(const double &src[], const int basisP, const double mult, const int avg, const bool sampleStd, double &out[]);
void Keltner(const double &h[], const double &l[], const double &c[], const int basisP, const int basisAvg, const int atrP, const int devMode, const int atrMode, const double mult, double &u[], double &b[], double &d[]);
void DonchianChannel(const double &h[], const double &l[], const int period, double &u[], double &m[], double &d[]);
void DonchianWidth(const double &h[], const double &l[], const int period, double &out[]);
void DonchianPercentWidth(const double &h[], const double &l[], const int period, double &out[]);
void ParkinsonVol(const double &h[], const double &l[], const int period, const bool annualize, const double tpy, double &out[]);
void GarmanKlassVol(const double &o[], const double &h[], const double &l[], const double &c[], const int period, const bool annualize, const double tpy, double &out[]);
void RogersSatchellVol(const double &o[], const double &h[], const double &l[], const double &c[], const int period, const bool annualize, const double tpy, double &out[]);
void YangZhangVol(const double &o[], const double &h[], const double &l[], const double &c[], const int period, const bool annualize, const double tpy, double &out[]);
void ChaikinVol(const double &h[], const double &l[], const int emaPeriod, const int rocPeriod, const int avg, const bool scaleTo100, double &out[]);
```

---

**Version:** 1.0  
**Status:** Approved for implementation  
