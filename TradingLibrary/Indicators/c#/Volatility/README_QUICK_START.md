# Indicators/Volatility - Quick Start Guide

## 📦 Установка

```bash
# Копируем все .cs файлы в проект
# Компилируем в DLL
csc /target:library /out:TradingLibrary.Indicators.dll *.cs /reference:TradingLibrary.Core.dll
```

## 🚀 Быстрый старт

### 1. ATR (Average True Range)

```csharp
using TradingLibrary.Indicators.Volatility;

// Batch обработка
double[] atr = ATR.Calculate(high, low, close, period: 14, mode: ATR.AtrMode.RMA);

// Streaming обновления
var atrCalc = new ATR.ATRCalculator(14, ATR.AtrMode.RMA);
foreach (var bar in bars)
{
    double value = atrCalc.Update(bar.High, bar.Low, bar.Close);
    if (atrCalc.IsReady)
        Console.WriteLine($"ATR: {value:F5}");
}
```

### 2. Bollinger Bands

```csharp
// Batch обработка
var (upper, basis, lower) = BollingerBands.Calculate(
    close, 
    basisPeriod: 20, 
    devMult: 2.0
);

// С дополнительными метриками
double[] percentB = BollingerBands.PercentB(close, 20, 2.0);
double[] bandwidth = BollingerBands.BandWidth(close, 20, 2.0);

// Streaming обновления
var bbCalc = new BollingerBands.BollingerCalculator(20, 2.0);
foreach (var bar in bars)
{
    var result = bbCalc.Update(bar.Close);
    if (bbCalc.IsReady)
    {
        Console.WriteLine($"BB: [{result.Lower:F5}, {result.Basis:F5}, {result.Upper:F5}]");
        Console.WriteLine($"PercentB: {bbCalc.PercentB:F3}, BandWidth: {bbCalc.BandWidth:F3}");
    }
}
```

### 3. Keltner Channels

```csharp
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

// Batch обработка
var (upper, basis, lower) = KeltnerChannels.Calculate(
    high, low, close,
    basisPeriod: 20,
    basisAvg: AvgMode.EMA,
    atrPeriod: 10,
    devMode: KeltnerChannels.AtrDevMode.ATR,
    mult: 2.0
);

// Streaming обновления
var keltnerCalc = new KeltnerChannels.KeltnerCalculator(
    basisPeriod: 20,
    basisAvg: AvgMode.EMA,
    atrPeriod: 10,
    devMode: KeltnerChannels.AtrDevMode.ATR,
    mult: 2.0
);

foreach (var bar in bars)
{
    var result = keltnerCalc.Update(bar.High, bar.Low, bar.Close);
    if (keltnerCalc.IsReady)
        Console.WriteLine($"Keltner: [{result.Lower:F5}, {result.Basis:F5}, {result.Upper:F5}]");
}
```

### 4. Historical Volatility

```csharp
// Batch обработка - annualized volatility
double[] hv = StdDevIndicator.HistoricalVolatility(
    close, 
    period: 20, 
    annualize: true, 
    tradingDaysPerYear: 252
);

// Streaming обновления
var hvCalc = new StdDevIndicator.HistoricalVolatilityCalculator(20, annualize: true);
foreach (var bar in bars)
{
    double vol = hvCalc.Update(bar.Close);
    if (hvCalc.IsReady)
        Console.WriteLine($"HV (annualized): {vol * 100:F2}%");
}
```

### 5. Advanced Estimators

```csharp
// Parkinson - range-based estimator
double[] parkinson = ParkinsonVolatility.Calculate(high, low, period: 20, annualize: true);

// Garman-Klass - OHLC estimator
double[] gk = GarmanKlassVolatility.Calculate(open, high, low, close, period: 20);

// Rogers-Satchell - drift-independent
double[] rs = RogersSatchellVolatility.Calculate(open, high, low, close, period: 20);

// Yang-Zhang - most comprehensive
double[] yz = YangZhangVolatility.Calculate(open, high, low, close, period: 20);

// Streaming Yang-Zhang
var yzCalc = new YangZhangVolatility.YangZhangCalculator(20, annualize: true);
foreach (var bar in bars)
{
    double vol = yzCalc.Update(bar.Open, bar.High, bar.Low, bar.Close);
    if (yzCalc.IsReady)
        Console.WriteLine($"Yang-Zhang Vol: {vol * 100:F2}%");
}
```

### 6. Donchian Channel

```csharp
// Batch обработка
var (upper, mid, lower) = DonchianChannel.Calculate(high, low, period: 20);
double[] width = DonchianChannel.Width(high, low, period: 20);
double[] percentWidth = DonchianChannel.PercentWidth(high, low, period: 20);

// Streaming обновления
var donchianCalc = new DonchianChannel.DonchianCalculator(20);
foreach (var bar in bars)
{
    var result = donchianCalc.Update(bar.High, bar.Low);
    if (donchianCalc.IsReady)
    {
        Console.WriteLine($"Donchian: [{result.Lower:F5}, {result.Mid:F5}, {result.Upper:F5}]");
        Console.WriteLine($"Width: {donchianCalc.Width:F5}, %Width: {donchianCalc.PercentWidth:F3}");
    }
}
```

### 7. Z-Score (Standardization)

```csharp
// Batch обработка
double[] zscore = StdDevIndicator.ZScore(close, period: 20, sample: true);

// Streaming обновления
var zCalc = new StdDevIndicator.ZScoreCalculator(20, sample: true);
foreach (var bar in bars)
{
    double z = zCalc.Update(bar.Close);
    if (zCalc.IsReady)
    {
        if (z > 2.0)
            Console.WriteLine($"Price is 2+ std deviations above mean: {z:F2}");
        else if (z < -2.0)
            Console.WriteLine($"Price is 2+ std deviations below mean: {z:F2}");
    }
}
```

### 8. Chaikin Volatility

```csharp
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

// Batch обработка
double[] cv = ChaikinVolatility.Calculate(
    high, low,
    emaPeriod: 10,
    rocPeriod: 10,
    avgMode: AvgMode.EMA,
    scaleTo100: true
);

// Streaming обновления
var cvCalc = new ChaikinVolatility.ChaikinCalculator(10, 10, AvgMode.EMA, scaleTo100: true);
foreach (var bar in bars)
{
    double value = cvCalc.Update(bar.High, bar.Low);
    if (cvCalc.IsReady)
    {
        if (value > 0)
            Console.WriteLine($"Volatility increasing: +{value:F2}%");
        else
            Console.WriteLine($"Volatility decreasing: {value:F2}%");
    }
}
```

## 🎯 Типичные сценарии использования

### Volatility Breakout Strategy

```csharp
var atrCalc = new ATR.ATRCalculator(14);
var bbCalc = new BollingerBands.BollingerCalculator(20, 2.0);

foreach (var bar in bars)
{
    double atr = atrCalc.Update(bar.High, bar.Low, bar.Close);
    var bb = bbCalc.Update(bar.Close);
    
    if (!atrCalc.IsReady || !bbCalc.IsReady) continue;
    
    // Low volatility squeeze detection
    if (bbCalc.BandWidth < 0.02) // BandWidth < 2%
    {
        Console.WriteLine("Volatility squeeze detected - breakout imminent");
    }
    
    // Breakout confirmation with ATR expansion
    if (bbCalc.PercentB > 1.0 && atr > previousAtr * 1.5)
    {
        Console.WriteLine("Bullish breakout with ATR expansion");
    }
}
```

### Multi-Timeframe Volatility Analysis

```csharp
// Create calculators for different timeframes
var atr1m = new ATR.ATRCalculator(14);
var atr5m = new ATR.ATRCalculator(14);
var atr15m = new ATR.ATRCalculator(14);

// Update each on respective bar closes
// Compare normalized volatility across timeframes
double natr1m = atr1m.Value / currentPrice;
double natr5m = atr5m.Value / currentPrice;
double natr15m = atr15m.Value / currentPrice;

if (natr1m > natr5m && natr5m > natr15m)
{
    Console.WriteLine("Volatility increasing across all timeframes");
}
```

### Volatility-Based Position Sizing

```csharp
var atrCalc = new ATR.ATRCalculator(14);
var hvCalc = new StdDevIndicator.HistoricalVolatilityCalculator(20, annualize: true);

foreach (var bar in bars)
{
    double atr = atrCalc.Update(bar.High, bar.Low, bar.Close);
    double hv = hvCalc.Update(bar.Close);
    
    if (!atrCalc.IsReady || !hvCalc.IsReady) continue;
    
    // Risk-based position sizing
    double accountRisk = 0.02; // 2% account risk
    double stopLoss = 2.0 * atr; // Stop at 2×ATR
    double positionSize = (accountBalance * accountRisk) / stopLoss;
    
    Console.WriteLine($"Position size: {positionSize:F2} units (ATR-based)");
    Console.WriteLine($"Annual volatility: {hv * 100:F2}%");
}
```

## 📝 Важные замечания

### Warm-up Period
Все индикаторы требуют warm-up период перед выдачей валидных значений:

```csharp
var calc = new ATR.ATRCalculator(14);

// Проверка готовности
if (calc.IsReady)
{
    // Индикатор готов к использованию
}

// Проверка оставшихся баров
Console.WriteLine($"Bars until ready: {calc.WarmupBarsLeft}");
```

### NaN Handling
Индикаторы возвращают `double.NaN` для невалидных значений:

```csharp
double value = calc.Update(high, low, close);

if (double.IsNaN(value))
{
    // Еще не готов или невалидные входные данные
}
else
{
    // Валидное значение
}
```

### Reset для переиспользования

```csharp
var calc = new ATR.ATRCalculator(14);

// Использование на первом инструменте
foreach (var bar in instrument1Bars)
{
    calc.Update(bar.High, bar.Low, bar.Close);
}

// Reset для второго инструмента
calc.Reset();

foreach (var bar in instrument2Bars)
{
    calc.Update(bar.High, bar.Low, bar.Close);
}
```

## ⚠️ Частые ошибки

### ❌ Использование значений до warm-up

```csharp
var calc = new ATR.ATRCalculator(14);
double value = calc.Update(h, l, c);
// value может быть NaN!
```

### ✅ Правильная проверка

```csharp
var calc = new ATR.ATRCalculator(14);
double value = calc.Update(h, l, c);
if (calc.IsReady && !double.IsNaN(value))
{
    // Безопасно использовать
}
```

### ❌ Забыт namespace alias

```csharp
// Ошибка компиляции
var calc = new BollingerCalculator(20, 2.0, AvgMode.EMA);
```

### ✅ Правильный import

```csharp
using AvgMode = TradingLibrary.Core.MovingAveragesExtensions.AvgMode;

var calc = new BollingerCalculator(20, 2.0, AvgMode.EMA);
```

## 📚 Дополнительная информация

- **Полная документация:** См. `VOLATILITY_IMPLEMENTATION_SUMMARY.md`
- **Архитектура:** См. `STRUCTURE_DIAGRAM.txt`
- **Спецификация:** См. `Volatility_Spec_v1_0.md` в проекте
- **API Reference:** См. XML комментарии в коде

## 🤝 Поддержка

При возникновении вопросов обращайтесь к:
1. XML документации в исходном коде
2. Файлу VOLATILITY_IMPLEMENTATION_SUMMARY.md
3. Спецификации Volatility_Spec_v1_0.md
