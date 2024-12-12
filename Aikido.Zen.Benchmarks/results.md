BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4515)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-QLRZWF : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-TJWCCB : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256
  Job-PFRYTQ : .NET 8.0.11, X64 NativeAOT AVX2

IterationCount=5  WarmupCount=1

| Method                          | Runtime            | PayloadSize | Mean           | Error           | StdDev         | Ratio | RatioSD |
|-------------------------------- |------------------- |------------ |---------------:|----------------:|---------------:|------:|--------:|
| ProcessJsonRequest              | .NET 8.0           | 1           |       1.833 us |       0.4367 us |      0.1134 us |  1.00 |    0.08 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1           |       3.630 us |       0.3165 us |      0.0822 us |  1.99 |    0.13 |
| ProcessJsonRequest              | NativeAOT 8.0      | 1           |             NA |              NA |             NA |     ? |       ? |
|                                 |                    |             |                |                 |                |       |         |
| ProcessXmlRequest               | .NET 8.0           | 1           |       3.100 us |       0.4617 us |      0.0715 us |  1.00 |    0.03 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1           |       5.360 us |       0.2437 us |      0.0633 us |  1.73 |    0.04 |
| ProcessXmlRequest               | NativeAOT 8.0      | 1           |       4.176 us |       1.1792 us |      0.3062 us |  1.35 |    0.09 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessFormRequest              | .NET 8.0           | 1           |       3.051 us |       0.3689 us |      0.0571 us |  1.00 |    0.02 |
| ProcessFormRequest              | .NET Framework 4.8 | 1           |       5.478 us |       0.0814 us |      0.0211 us |  1.80 |    0.03 |
| ProcessFormRequest              | NativeAOT 8.0      | 1           |       3.672 us |       0.1787 us |      0.0464 us |  1.20 |    0.02 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1           |     233.259 us |      15.8999 us |      4.1292 us |  1.00 |    0.02 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1           |     267.944 us |      43.6753 us |      6.7588 us |  1.15 |    0.03 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 1           |   1,443.608 us |     111.6298 us |     17.2748 us |  6.19 |    0.12 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessJsonRequest              | .NET 8.0           | 10          |       3.989 us |       0.7156 us |      0.1107 us |  1.00 |    0.04 |
| ProcessJsonRequest              | .NET Framework 4.8 | 10          |      10.073 us |       0.8595 us |      0.2232 us |  2.53 |    0.08 |
| ProcessJsonRequest              | NativeAOT 8.0      | 10          |             NA |              NA |             NA |     ? |       ? |
|                                 |                    |             |                |                 |                |       |         |
| ProcessXmlRequest               | .NET 8.0           | 10          |       9.234 us |      10.4210 us |      1.6127 us |  1.02 |    0.21 |
| ProcessXmlRequest               | .NET Framework 4.8 | 10          |      14.915 us |       0.6940 us |      0.1802 us |  1.65 |    0.22 |
| ProcessXmlRequest               | NativeAOT 8.0      | 10          |      11.815 us |       1.9045 us |      0.2947 us |  1.31 |    0.18 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessFormRequest              | .NET 8.0           | 10          |       9.842 us |      16.5721 us |      2.5645 us |  1.04 |    0.32 |
| ProcessFormRequest              | .NET Framework 4.8 | 10          |      14.861 us |       1.3912 us |      0.3613 us |  1.58 |    0.31 |
| ProcessFormRequest              | NativeAOT 8.0      | 10          |      11.754 us |       0.2325 us |      0.0604 us |  1.25 |    0.24 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 10          |   1,358.529 us |      57.8261 us |      8.9486 us |  1.00 |    0.01 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 10          |   2,438.108 us |      66.6348 us |     10.3118 us |  1.79 |    0.01 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 10          |   3,631.333 us |   1,175.2407 us |    305.2063 us |  2.67 |    0.21 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessJsonRequest              | .NET 8.0           | 100         |      26.381 us |       2.1631 us |      0.5617 us |  1.00 |    0.03 |
| ProcessJsonRequest              | .NET Framework 4.8 | 100         |      76.414 us |       4.1412 us |      1.0755 us |  2.90 |    0.07 |
| ProcessJsonRequest              | NativeAOT 8.0      | 100         |             NA |              NA |             NA |     ? |       ? |
|                                 |                    |             |                |                 |                |       |         |
| ProcessXmlRequest               | .NET 8.0           | 100         |     218.734 us |      34.9552 us |      5.4094 us |  1.00 |    0.03 |
| ProcessXmlRequest               | .NET Framework 4.8 | 100         |     357.530 us |      18.1859 us |      4.7228 us |  1.64 |    0.04 |
| ProcessXmlRequest               | NativeAOT 8.0      | 100         |     351.033 us |      43.4718 us |     11.2895 us |  1.61 |    0.06 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessFormRequest              | .NET 8.0           | 100         |     207.549 us |       9.3869 us |      2.4377 us |  1.00 |    0.02 |
| ProcessFormRequest              | .NET Framework 4.8 | 100         |     350.823 us |      40.8341 us |      6.3191 us |  1.69 |    0.03 |
| ProcessFormRequest              | NativeAOT 8.0      | 100         |     351.326 us |      39.3553 us |      6.0903 us |  1.69 |    0.03 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 100         |  26,406.823 us |   3,625.3934 us |    561.0336 us |  1.00 |    0.03 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 100         |  45,209.405 us |  21,721.9446 us |  5,641.1200 us |  1.71 |    0.20 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 100         |  28,302.196 us |   3,127.9401 us |    484.0522 us |  1.07 |    0.03 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessJsonRequest              | .NET 8.0           | 1000        |     286.626 us |      43.7391 us |      6.7687 us |  1.00 |    0.03 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1000        |     776.727 us |      59.2855 us |     15.3963 us |  2.71 |    0.08 |
| ProcessJsonRequest              | NativeAOT 8.0      | 1000        |             NA |              NA |             NA |     ? |       ? |
|                                 |                    |             |                |                 |                |       |         |
| ProcessXmlRequest               | .NET 8.0           | 1000        |  15,868.864 us |     938.7902 us |    243.8008 us |  1.00 |    0.02 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1000        |  27,478.219 us |   2,820.0628 us |    436.4078 us |  1.73 |    0.03 |
| ProcessXmlRequest               | NativeAOT 8.0      | 1000        |  30,139.656 us |   2,241.4709 us |    582.1029 us |  1.90 |    0.04 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessFormRequest              | .NET 8.0           | 1000        |  16,506.564 us |   1,743.6363 us |    452.8168 us |  1.00 |    0.04 |
| ProcessFormRequest              | .NET Framework 4.8 | 1000        |  26,902.496 us |   2,764.5592 us |    427.8186 us |  1.63 |    0.05 |
| ProcessFormRequest              | NativeAOT 8.0      | 1000        |  29,720.329 us |   3,203.1742 us |    831.8542 us |  1.80 |    0.06 |
|                                 |                    |             |                |                 |                |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1000        | 387,892.020 us | 105,566.4831 us | 27,415.2805 us |  1.00 |    0.09 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1000        | 750,677.360 us |  32,039.6264 us |  8,320.5893 us |  1.94 |    0.12 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 1000        | 325,241.080 us |  46,751.2651 us | 12,141.1551 us |  0.84 |    0.06 |