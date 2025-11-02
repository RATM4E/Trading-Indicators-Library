# SharedLibrary/Indicators — Extended Summary (v1.0)

This document summarizes all indicator modules under `Indicators/`, describing for each indicator its **purpose**, **practical use**, and **synergistic combinations** (what other indicators it pairs with and what task that combination solves).

---

## 🧭 1. Trend Indicators — *TrendIndicators.cs/.mqh*

### SuperTrend
**Purpose:** follows price trend using ATR-based volatility bands.  
**Use:** entry/exit baseline; trailing stop; volatility filter.  
**Combines well with:**
- **ADX/ER** → confirm trend strength.  
- **VWAP** → intraday dynamic anchor; SuperTrend break + VWAP bias = strong signal.  
- **ATR Bands / Volatility Cluster** → adaptive trailing under varying volatility.

### Parabolic SAR
**Purpose:** trend-following reversal tracker.  
**Use:** exit logic; “flip” signal for swing systems.  
**Combines with:**
- **SuperTrend** → cross‑confirmation of reversals.  
- **RSI or MFI** → filter false SAR flips in overbought/oversold zones.

### Ichimoku
**Purpose:** holistic trend & momentum framework.  
**Use:** regime detection, mid‑term trend direction, cloud‑based support/resistance.  
**Combines with:**
- **ADX/ER/CHOP** → confirm cloud breaks only in strong‑trend regimes.  
- **VWAP** → adds dynamic equilibrium reference inside Ichimoku zones.

### MA Ribbons (SMA, EMA, WMA, HMA, TEMA)
**Purpose:** smoothing and multi‑horizon trend structure.  
**Use:** basis for crossover logic, adaptive filters, and baseline in regression models.  
**Combines with:**
- **MACD / PPO** → detect acceleration/deceleration.  
- **Volatility metrics (ATR, CHOP)** → tune MA length adaptively.  
- **Kaufman ER** → dynamic smoothing (adaptive MA).

---

## ⚡ 2. Momentum Indicators — *Momentum.cs/.mqh*

### RSI (Relative Strength Index)
**Purpose:** oscillator of price velocity; measures internal momentum.  
**Use:** overbought/oversold detection, divergence analysis.  
**Combines with:**
- **MACD / AO** → confirm divergence.  
- **ATR / Volatility** → normalize thresholds for different regimes.  
- **VWAP / Bands** → RSI extremes near VWAP edges = mean‑reversion entries.

### Stochastic (%K, %D)
**Purpose:** momentum of position within recent range.  
**Use:** range‑bound trading, pullback entries in trends.  
**Combines with:**
- **SuperTrend** → take Stoch oversold only in up‑trend.  
- **ADX** → disable in non‑trending markets.

### CCI (Commodity Channel Index)
**Purpose:** deviation of price from its moving average in σ‑units.  
**Use:** detect momentum bursts and reversals.  
**Combines with:**
- **ATR / Bollinger** → volatility‑adjusted breakouts.  
- **VWAP** → strong CCI divergence near VWAP center → reversion signal.

### Williams %R
**Purpose:** normalized oscillator (range position).  
**Use:** timing pullbacks.  
**Combines with:** **Choppiness** → more effective in flat regimes.

### Momentum / ROC
**Purpose:** raw rate of change.  
**Combines with:** **Regression slope / R²** → linear trend acceleration check.

---

## 🎛 3. Oscillators — *Oscillators.cs/.mqh*

### MACD / ZMACD / PPO / PO
**Purpose:** dual‑MA momentum; visualize trend acceleration.  
**Use:** crossovers, momentum confirmation, volatility squeeze detection.  
**Combines with:**
- **RSI** → double‑confirmation of momentum reversals.  
- **ADX** → trade MACD crosses only in trending regimes.  
- **VWAP** → entry alignment with VWAP slope.  
- **SuperTrend** → for continuation confirmation.

### Awesome / Accelerator Oscillator
**Purpose:** Bill Williams momentum‑trend composite.  
**Use:** sequential bar color pattern for continuation/early reversal.  
**Combines with:** **Fractals / SuperTrend / Alligator** (for multi‑phase setups).

### TRIX / PMO / KST
**Purpose:** triple‑smoothed momentum; cycle‑filtering.  
**Use:** long‑term momentum direction, early turns before price crossovers.  
**Combines with:**
- **Regression slope / R²** → confirm statistically significant direction.  
- **CHOP/MMI** → avoid use in choppy regimes.

### Ultimate Oscillator
**Purpose:** volume‑weighted momentum integrating short/long horizons.  
**Combines with:** **OBV / MFI / CMF** → volume confirmation layer.

### Fisher Transform
**Purpose:** Gaussian‑normalized price transform; sharp turning‑point detector.  
**Combines with:** **RSI / MACD / ATR Bands** → detect “phase shift” before breakouts.

---

## 🌊 4. Volatility Indicators — *Volatility.cs/.mqh*

### ATR / NATR
**Purpose:** range‑based volatility estimator.  
**Use:** dynamic stop sizing, position sizing, adaptive filters.  
**Combines with:**
- **SuperTrend / Keltner / Bollinger** → envelope width source.  
- **VWAP** → ATR deviation bands.  
- **ER / CHOP / MMI** → adapt ATR period to regime.

### StdDev / Z‑Score / Historical Volatility
**Purpose:** dispersion metrics; normalize price or returns.  
**Use:** z‑score scaling, volatility normalization for ML models.  
**Combines with:** **Bands / VWAP / FRVP** → detect stretched deviations.

### Bollinger Bands
**Purpose:** volatility envelope with mean‑reversion implication.  
**Use:** breakout or reversion trading.  
**Combines with:**
- **RSI / CCI** → mean‑reversion confirmation.  
- **ATR / CHOP** → adapt σ multiplier to regime.  
- **VWAP** → nested “Bollinger‑VWAP” hybrid channel.

### Keltner Channels
**Purpose:** ATR‑based adaptive envelope.  
**Use:** volatility breakout; dynamic support/resistance.  
**Combines with:** **ADX** (trend confirmation) or **CHOP** (avoid false breakouts).

### Donchian Channel
**Purpose:** range breakout; volatility squeeze expansion.  
**Combines with:** **ATR / SuperTrend / DMI** → classical trend‑following entry logic.

### Advanced Vol Metrics (Parkinson, Garman‑Klass, RS, YZ)
**Purpose:** statistical volatility estimators using OHLC.  
**Use:** portfolio volatility forecasts; adaptive position sizing.  
**Combines with:** **RiskMetrics / Sharpe / Sortino** in Trading/ module.

### Chaikin Volatility
**Purpose:** EMA(H−L) momentum; detects volatility bursts.  
**Combines with:** **Volume / MFI / OBV** → confirm activity spikes.

---

## 📈 5. Volume Indicators — *Volume.cs/.mqh*

### OBV (On‑Balance Volume)
**Purpose:** cumulative volume pressure.  
**Use:** confirm trend validity.  
**Combines with:** **Price trend (MA/SuperTrend)** → trend confirmation.

### Accumulation/Distribution & CMF
**Purpose:** price‑volume confirmation of money flow.  
**Use:** validate breakouts; detect accumulation before reversals.  
**Combines with:** **RSI / VWAP / MACD** → spot divergence in volume vs price.

### MFI (Money Flow Index)
**Purpose:** RSI weighted by volume.  
**Use:** momentum + volume strength; divergence.  
**Combines with:** **ATR / VWAP / Bollinger** → mean‑reversion filtering.

### VWAP (Anchored / Rolling)
**Purpose:** fair‑value benchmark; institutional anchor.  
**Use:** equilibrium bias, trend bias filter, and trade location.  
**Combines with:**  
- **SuperTrend / MACD / RSI** → directional bias filters.  
- **Bands / ATR** → dynamic σ envelopes (VWAP ± σ×ATR).

### FRVP / RVP (Fixed / Rolling Volume Profile)
**Purpose:** map volume by price; identify POC, VAH/VAL, VWAP, modes.  
**Use:** structural support/resistance detection, regime anchors.  
**Combines with:**  
- **VWAP** → alignment of volume & value.  
- **Cluster Search** → volume hot‑spot confirmation.

### Cluster Search (LTF‑Derived)
**Purpose:** identify intrabar volume spikes on lower timeframe.  
**Use:** detect micro‑clusters for swing/trend initiation.  
**Combines with:** **FRVP / VWAP / ATR** → confirm break area’s liquidity concentration.

---

## 🧠 6. Market / Regime Indicators — *Market.cs/.mqh*

### ADX / ADXR / DMI / DMI‑Osc
**Purpose:** strength and direction of trend.  
**Use:** regime filter, entry timing.  
**Combines with:** **SuperTrend / MACD / ATR** → validate strong trends only.

### Aroon
**Purpose:** recency of highs/lows → trend age.  
**Use:** early trend detection.  
**Combines with:** **ADX / SuperTrend** → confirm new trend starts.

### Vortex
**Purpose:** detect directional dominance.  
**Combines with:** **RWI / ER** → directional persistence confirmation.

### RWI (Random Walk Index)
**Purpose:** detect deviation from random walk; trend validity.  
**Combines with:** **MMI / iVAR** → confirm true trend regime.

### Efficiency Ratio (ER)
**Purpose:** smoothness of movement; adaptive parameter driver.  
**Combines with:** **ATR / MA / KAMA / CHOP** → adjust sensitivity dynamically.

### Trend Intensity Index (TII)
**Purpose:** fraction of closes above baseline; measures conviction.  
**Combines with:** **SuperTrend / ER / R²** → confirm directional quality.

### Regression Slope / R²
**Purpose:** quantify linear trend and its quality.  
**Combines with:** **ADX / MACD / ER** → build objective “trend quality” score.

### Choppiness Index (CHOP)
**Purpose:** trend vs consolidation discriminator.  
**Combines with:** **MMI / ER / iVAR** → regime detection; disable trend logic in high CHOP.

### Market Meanness Index (MMI)
**Purpose:** statistical measure of randomness in price path.  
**Combines with:** **CHOP / iVAR / RWI** → refined trend/chop classifier.

### iVAR (Index of Variability)
**Purpose:** fractal variability index; inverse of Hurst exponent.  
**Use:** detect fractal persistence, trend quality, or chaos.  
**Combines with:**  
- **MMI / CHOP** → composite regime detector.  
- **ATR / SuperTrend** → adaptive volatility scaling by persistence.

---

## 🔩 Cross‑category Combos & Use Cases

| Task | Typical Combination | Goal |
|------|---------------------|------|
| **Trend Entry Confirmation** | SuperTrend + ADX + VWAP + RSI | Trade only strong, aligned trends. |
| **Volatility Breakout** | Keltner + ADX + Chaikin Volatility | Detect volatility expansion. |
| **Mean‑Reversion Entry** | Bollinger + RSI + MFI + VWAP | Buy/sell at stretched deviations near VWAP. |
| **Regime Detection (Trend/Chop)** | ER + CHOP + MMI + iVAR | Classify market phase for strategy switching. |
| **Adaptive Stop Sizing** | ATR + ER + VWAP Bands | Scale SL to volatility & trend efficiency. |
| **Volume Structure Bias** | FRVP + VWAP + ClusterSearch | Trade around volume clusters near VWAP equilibrium. |
| **Trend Quality Scoring** | R² + ER + ADX + iVAR | Quantify how “clean” and strong the trend is. |
| **Breakout Confirmation** | Donchian + CMF + ADX + OBV | Confirm breakouts with directional volume. |

---

**Version:** 1.0  
**Status:** Reference Summary (for documentation & design use)
