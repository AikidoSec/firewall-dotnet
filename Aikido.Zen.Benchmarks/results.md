BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4515)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-SIKRSX : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-HTQDYP : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256
  Job-RKLRKY : .NET 8.0.11, X64 NativeAOT AVX2

IterationCount=5  WarmupCount=1

| Method                          | Runtime            | PayloadSize | Mean             | Error           | StdDev         | Ratio | RatioSD |
|-------------------------------- |------------------- |------------ |-----------------:|----------------:|---------------:|------:|--------:|
| ProcessJsonRequest              | .NET 8.0           | 1           |         1.804 us |       0.6418 us |      0.0993 us |  1.00 |    0.07 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1           |         3.521 us |       0.2250 us |      0.0584 us |  1.96 |    0.10 |
| ProcessJsonRequest              | NativeAOT 8.0      | 1           |               NA |              NA |
NA |     ? |       ? |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessXmlRequest               | .NET 8.0           | 1           |         2.870 us |       0.6527 us |      0.1010 us |  1.00 |    0.04 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1           |         5.382 us |       1.4984 us |      0.2319 us |  1.88 |    0.09 |
| ProcessXmlRequest               | NativeAOT 8.0      | 1           |         3.533 us |       0.3675 us |      0.0954 us |  1.23 |    0.05 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessFormRequest              | .NET 8.0           | 1           |         3.039 us |       0.2756 us |      0.0427 us |  1.00 |    0.02 |
| ProcessFormRequest              | .NET Framework 4.8 | 1           |         5.328 us |       0.6471 us |      0.1001 us |  1.75 |    0.04 |
| ProcessFormRequest              | NativeAOT 8.0      | 1           |         3.593 us |       0.2605 us |      0.0677 us |  1.18 |    0.03 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1           |     3,051.654 us |     107.1998 us |     27.8394 us |  1.00 |    0.01 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1           |     2,821.614 us |      67.0854 us |     17.4219 us |  0.92 |    0.01 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 1           |     6,889.100 us |     110.6131 us |     28.7259 us |  2.26 |    0.02 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessJsonRequest              | .NET 8.0           | 10          |         3.976 us |       0.5148 us |      0.0797 us |  1.00 |    0.03 |
| ProcessJsonRequest              | .NET Framework 4.8 | 10          |        10.426 us |       1.1197 us |      0.1733 us |  2.62 |    0.06 |
| ProcessJsonRequest              | NativeAOT 8.0      | 10          |               NA |              NA |
NA |     ? |       ? |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessXmlRequest               | .NET 8.0           | 10          |         8.497 us |       0.4109 us |      0.0636 us |  1.00 |    0.01 |
| ProcessXmlRequest               | .NET Framework 4.8 | 10          |        14.799 us |       1.3038 us |      0.3386 us |  1.74 |    0.04 |
| ProcessXmlRequest               | NativeAOT 8.0      | 10          |        11.653 us |       0.5254 us |      0.1364 us |  1.37 |    0.02 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessFormRequest              | .NET 8.0           | 10          |        11.330 us |      16.4429 us |      2.5446 us |  1.04 |    0.31 |
| ProcessFormRequest              | .NET Framework 4.8 | 10          |        15.651 us |       0.3236 us |      0.0840 us |  1.44 |    0.30 |
| ProcessFormRequest              | NativeAOT 8.0      | 10          |        11.293 us |       1.0357 us |      0.2690 us |  1.04 |    0.22 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 10          |    23,161.948 us |   7,300.6241 us |  1,129.7797 us |  1.00 |    0.06 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 10          |    31,862.609 us |     911.0583 us |    236.5990 us |  1.38 |    0.06 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 10          |    31,639.575 us |   3,610.0453 us |    937.5173 us |  1.37 |    0.07 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessJsonRequest              | .NET 8.0           | 100         |        25.586 us |       2.2690 us |      0.5892 us |  1.00 |    0.03 |
| ProcessJsonRequest              | .NET Framework 4.8 | 100         |        75.392 us |       7.2957 us |      1.8947 us |  2.95 |    0.09 |
| ProcessJsonRequest              | NativeAOT 8.0      | 100         |               NA |              NA |
NA |     ? |       ? |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessXmlRequest               | .NET 8.0           | 100         |       225.450 us |      90.9033 us |     23.6073 us |  1.01 |    0.13 |
| ProcessXmlRequest               | .NET Framework 4.8 | 100         |       351.747 us |      24.8502 us |      6.4535 us |  1.57 |    0.15 |
| ProcessXmlRequest               | NativeAOT 8.0      | 100         |       350.129 us |      26.0377 us |      6.7619 us |  1.57 |    0.15 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessFormRequest              | .NET 8.0           | 100         |       213.300 us |      60.0658 us |     15.5989 us |  1.00 |    0.09 |
| ProcessFormRequest              | .NET Framework 4.8 | 100         |       350.033 us |      18.3096 us |      4.7549 us |  1.65 |    0.11 |
| ProcessFormRequest              | NativeAOT 8.0      | 100         |       347.319 us |       3.0327 us |      0.4693 us |  1.64 |    0.11 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 100         |   182,810.520 us |  17,083.4367 us |  4,436.5143 us |  1.00 |    0.03 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 100         |   483,072.800 us |  67,622.7837 us | 17,561.4222 us |  2.64 |    0.11 |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 100         |   189,300.100 us |   8,645.9901 us |  2,245.3362 us |  1.04 |    0.03 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessJsonRequest              | .NET 8.0           | 1000        |       275.498 us |      31.9329 us |      8.2929 us |  1.00 |    0.04 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1000        |               NA |              NA |
NA |     ? |       ? |
| ProcessJsonRequest              | NativeAOT 8.0      | 1000        |               NA |              NA |
NA |     ? |       ? |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessXmlRequest               | .NET 8.0           | 1000        |    16,716.386 us |   1,178.3812 us |    306.0219 us |  1.00 |    0.02 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1000        |               NA |              NA |
NA |     ? |       ? |
| ProcessXmlRequest               | NativeAOT 8.0      | 1000        |    30,534.315 us |   1,078.5384 us |    280.0930 us |  1.83 |    0.03 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessFormRequest              | .NET 8.0           | 1000        |    17,538.551 us |     693.5234 us |    180.1058 us |  1.00 |    0.01 |
| ProcessFormRequest              | .NET Framework 4.8 | 1000        |               NA |              NA |
NA |     ? |       ? |
| ProcessFormRequest              | NativeAOT 8.0      | 1000        |    29,707.001 us |   1,596.6904 us |    414.6554 us |  1.69 |    0.03 |
|                                 |                    |             |                  |                 |
   |       |         |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1000        | 1,583,221.375 us |  42,082.2288 us |  6,512.2715 us |  1.00 |    0.01 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1000        |               NA |              NA |
NA |     ? |       ? |
| ProcessMultipartFormDataRequest | NativeAOT 8.0      | 1000        | 1,977,292.025 us | 100,195.4369 us | 15,505.3547 us |  1.25 |    0.01 |
