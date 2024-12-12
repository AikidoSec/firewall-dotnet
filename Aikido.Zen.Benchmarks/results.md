## HttpHelper benchmark results

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4515)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-UKHEKM : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-CSUXNT : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256

IterationCount=5  WarmupCount=1

| Method                          | Runtime            | PayloadSize | Mean           | Ratio |
|-------------------------------- |------------------- |------------ |---------------:|------:|
| ProcessJsonRequest              | .NET 8.0           | 1           |       1.591 us |  1.00 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1           |       4.149 us |  2.61 |
|                                 |                    |             |                |       |
| ProcessXmlRequest               | .NET 8.0           | 1           |       7.157 us |  1.00 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1           |      22.627 us |  3.17 |
|                                 |                    |             |                |       |
| ProcessFormRequest              | .NET 8.0           | 1           |       8.196 us |  1.00 |
| ProcessFormRequest              | .NET Framework 4.8 | 1           |      20.768 us |  2.54 |
|                                 |                    |             |                |       |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1           |     605.969 us |  1.00 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1           |   1,658.171 us |  2.75 |
|                                 |                    |             |                |       |
| ProcessJsonRequest              | .NET 8.0           | 10          |       2.629 us |  1.01 |
| ProcessJsonRequest              | .NET Framework 4.8 | 10          |       7.082 us |  2.71 |
|                                 |                    |             |                |       |
| ProcessXmlRequest               | .NET 8.0           | 10          |      11.240 us |  1.01 |
| ProcessXmlRequest               | .NET Framework 4.8 | 10          |      29.229 us |  2.63 |
|                                 |                    |             |                |       |
| ProcessFormRequest              | .NET 8.0           | 10          |      12.765 us |  1.07 |
| ProcessFormRequest              | .NET Framework 4.8 | 10          |      21.726 us |  1.81 |
|                                 |                    |             |                |       |
| ProcessMultipartFormDataRequest | .NET 8.0           | 10          |   1,655.980 us |  1.00 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 10          |  17,730.042 us | 10.74 |
|                                 |                    |             |                |       |
| ProcessJsonRequest              | .NET 8.0           | 100         |      13.471 us |  1.00 |
| ProcessJsonRequest              | .NET Framework 4.8 | 100         |      43.114 us |  3.20 |
|                                 |                    |             |                |       |
| ProcessXmlRequest               | .NET 8.0           | 100         |      43.831 us |  1.00 |
| ProcessXmlRequest               | .NET Framework 4.8 | 100         |      83.946 us |  1.92 |
|                                 |                    |             |                |       |
| ProcessFormRequest              | .NET 8.0           | 100         |      37.920 us |  1.01 |
| ProcessFormRequest              | .NET Framework 4.8 | 100         |      72.615 us |  1.93 |
|                                 |                    |             |                |       |
| ProcessMultipartFormDataRequest | .NET 8.0           | 100         |  38,122.544 us |  1.00 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 100         |  62,859.038 us |  1.65 |
|                                 |                    |             |                |       |
| ProcessJsonRequest              | .NET 8.0           | 1000        |     122.269 us |  1.00 |
| ProcessJsonRequest              | .NET Framework 4.8 | 1000        |     405.012 us |  3.32 |
|                                 |                    |             |                |       |
| ProcessXmlRequest               | .NET 8.0           | 1000        |     342.472 us |  1.00 |
| ProcessXmlRequest               | .NET Framework 4.8 | 1000        |     610.450 us |  1.78 |
|                                 |                    |             |                |       |
| ProcessFormRequest              | .NET 8.0           | 1000        |     339.388 us |  1.00 |
| ProcessFormRequest              | .NET Framework 4.8 | 1000        |     583.277 us |  1.72 |
|                                 |                    |             |                |       |
| ProcessMultipartFormDataRequest | .NET 8.0           | 1000        | 297,367.450 us |  1.00 |
| ProcessMultipartFormDataRequest | .NET Framework 4.8 | 1000        | 401,241.375 us |  1.35 |
