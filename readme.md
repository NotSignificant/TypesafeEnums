# TypesafeEnums

## Overview

TypesafeEnums ist eine Bibliothek, die Enums im Java-Stil ermöglicht.

## Features

- Statische Switch-Methode(n), die die Switch-Anweisung für TSEnum-Objekte emuliert.
  Die Switch-Emulation kennt Multi-Cases (mehrere Cases teilen sich eine Aktion bzw.
  Funktion) und Switch-Expressions (siehe Beispiel-Code). Aktionen und Funktionen
  müssen parameterlos sein.
- Instaziierungen bei Erstbenutzung eines Enums.
- Interfaces können wie gewohnt implementiert werden, die abstrakten Methoden
  müssen jedoch per Delegates übergeben werden.
- TSEnum-Erweiterungen müssen sealed sein und einen privaten Konstruktor haben.
- Nur öffentliche statische Felder einer Enum-Klasse werden als Enum-Elemente akzeptiert,
  anders als bei dem Java-Vorbild dürfen hier jedoch auch andere statische Objekte im
  Konstruktor verwendet werden.

## Installation

Installation über NuGet.

## Usage
***Codebeispiel:***

``` csharp
using static TypesafeEnums.TSEnum;

namespace TypesafeEnums.Test
{
    internal sealed class Enum1 : TSEnum<Enum1>
    {
        public static readonly Enum1 Value1 = new();
        public static readonly Enum1 Value2 = new();
        public static readonly Enum1 Value3 = new();
        public static readonly Enum1 Value4 = new();

        private Enum1()
        {
        }
    }
    internal sealed class Enum2 : TSEnum<Enum2>
    {
        public static readonly Enum2 Value1 = new("value 1");
        public static readonly Enum2 Value2 = new("value 2");
        public static readonly Enum2 Value3 = new("value 3");

        public string Value { get;}

        private Enum2(string v)
        {
            Value = v;
        }
    }

    internal class Program
    {
        static void Main()
        {
            Enum1[] values1 = Enum1.Values();
            Enum2[] values2 = Enum2.Values();
            Switch(null, [
                Case([Enum1.Value1], () => Console.WriteLine("Enum1.Value1")),
                Case([Enum1.Value2], () => Console.WriteLine("Enum1.Value2")),
                Case([Enum1.Value3, Enum1.Value4], () => Console.WriteLine("Enum1.Value3|4")),
                Default<Enum1>(() => Console.WriteLine("NO MATCH")),
            ]);
            Console.WriteLine(Switch(null, [
                Case([Enum2.Value1], () => "Enum2.Value1"),
                Case([Enum2.Value2], () => "Enum2.Value2"),
                Case([Enum2.Value3], () => "Enum2.Value3"),
                Default<Enum2, string>(() => "NO MATCH"),
            ]));
        }
    }
}
```
## Analyzer

Das Paket enthält einen Codeanalyzer, der die Korrektheit der emulierten Switch-Statemens
überwacht (doppelte Cases und Defaults und ungültige Enum-Instanzen).

## Requirements

Unterstützt .NET-8.0+ und C#12+

## License

Siehe LICENSE.txt.
