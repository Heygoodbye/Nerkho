# Nerkho (نرخو)

A modern Persian-first Windows dashboard for live Iranian currency and gold prices.

## Features

- Live TGJU polling every second
- Five permanent cards: USD, EUR, GBP, AED, and 18K gold
- All ISO currencies exposed by the TGJU feed, with instant search
- Toman/Rial display switch
- Session chart with daily high/low and source time
- Borderless, rounded, resizable Windows UI
- Native currency glyphs and country flags
- Custom dark theme and slim interactive scrollbars
- Offline-safe behavior that keeps the last received prices visible

## Run from source

```powershell
dotnet run --project .\Nerkh\Nerkh.csproj
```

## Build

```powershell
dotnet build .\Nerkh.sln -c Release
dotnet publish .\Nerkh\Nerkh.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Prices come from `https://call3.tgju.org/ajax.json`. The UI defaults to Toman, so Rial values received from the feed are divided by 10. This software is informational and is not financial advice.
