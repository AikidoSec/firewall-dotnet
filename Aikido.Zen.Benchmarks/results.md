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

## SQL Injection detector benchmark results

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4580)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-DYKDGE : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-YASELY : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256

IterationCount=5  WarmupCount=1

| Method                              | Runtime            | Dialect    | Mean         | Ratio |
|------------------------------------ |------------------- |----------- |-------------:|------:|
| DetectSQLInjection                  | .NET 8.0           | Generic    |     7.312 ns |  1.00 |
| DetectSQLInjection                  | .NET Framework 4.8 | Generic    |    50.504 ns |  6.91 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongQuery     | .NET 8.0           | Generic    |   425.536 ns |  1.00 |
| DetectSQLInjectionWithLongQuery     | .NET Framework 4.8 | Generic    | 1,059.323 ns |  2.49 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongUserInput | .NET 8.0           | Generic    |   223.223 ns |  1.00 |
| DetectSQLInjectionWithLongUserInput | .NET Framework 4.8 | Generic    |   271.498 ns |  1.22 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithSafeInput     | .NET 8.0           | Generic    |     8.312 ns |  1.00 |
| DetectSQLInjectionWithSafeInput     | .NET Framework 4.8 | Generic    |    54.648 ns |  6.58 |
|                                     |                    |            |              |       |
| DetectSQLInjection                  | .NET 8.0           | MySQL      |     7.175 ns |  1.00 |
| DetectSQLInjection                  | .NET Framework 4.8 | MySQL      |    50.566 ns |  7.06 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongQuery     | .NET 8.0           | MySQL      |   417.567 ns |  1.00 |
| DetectSQLInjectionWithLongQuery     | .NET Framework 4.8 | MySQL      | 1,039.221 ns |  2.49 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongUserInput | .NET 8.0           | MySQL      |   228.404 ns |  1.00 |
| DetectSQLInjectionWithLongUserInput | .NET Framework 4.8 | MySQL      |   282.427 ns |  1.24 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithSafeInput     | .NET 8.0           | MySQL      |     8.652 ns |  1.00 |
| DetectSQLInjectionWithSafeInput     | .NET Framework 4.8 | MySQL      |    55.089 ns |  6.37 |
|                                     |                    |            |              |       |
| DetectSQLInjection                  | .NET 8.0           | PostgreSQL |     7.320 ns |  1.00 |
| DetectSQLInjection                  | .NET Framework 4.8 | PostgreSQL |    50.466 ns |  6.90 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongQuery     | .NET 8.0           | PostgreSQL |   458.920 ns |  1.00 |
| DetectSQLInjectionWithLongQuery     | .NET Framework 4.8 | PostgreSQL | 1,033.667 ns |  2.26 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithLongUserInput | .NET 8.0           | PostgreSQL |   227.417 ns |  1.00 |
| DetectSQLInjectionWithLongUserInput | .NET Framework 4.8 | PostgreSQL |   275.232 ns |  1.21 |
|                                     |                    |            |              |       |
| DetectSQLInjectionWithSafeInput     | .NET 8.0           | PostgreSQL |     8.675 ns |  1.00 |
| DetectSQLInjectionWithSafeInput     | .NET Framework 4.8 | PostgreSQL |    55.733 ns |  6.43 |

## Shell Injection detector benchmark results

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4580)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-DYKDGE : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-YASELY : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256

IterationCount=5  WarmupCount=1

| Method                                | Runtime            | Mean      | Ratio |
|-------------------------------------- |------------------- |----------:|------:|
| DetectShellInjection                  | .NET 8.0           |  84.41 ns |  1.00 |
| DetectShellInjection                  | .NET Framework 4.8 | 143.32 ns |  1.70 |
|                                       |                    |           |       |
| DetectShellInjectionWithLongCommand   | .NET 8.0           | 376.22 ns |  1.00 |
| DetectShellInjectionWithLongCommand   | .NET Framework 4.8 | 596.93 ns |  1.59 |
|                                       |                    |           |       |
| DetectShellInjectionWithLongUserInput | .NET 8.0           | 299.24 ns |  1.00 |
| DetectShellInjectionWithLongUserInput | .NET Framework 4.8 | 495.19 ns |  1.66 |
|                                       |                    |           |       |
| DetectShellInjectionWithSafeInput     | .NET 8.0           |  95.22 ns |  1.00 |
| DetectShellInjectionWithSafeInput     | .NET Framework 4.8 | 150.38 ns |  1.58 |

## Patches vs Unpatch HttpClient and WebRequest benchmark results

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22635.4580)
AMD Ryzen 7 5800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 [AttachedDebugger]
  Job-DYKDGE : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-YASELY : .NET Framework 4.8.1 (4.8.9282.0), X64 RyuJIT VectorSize=256

IterationCount=5  WarmupCount=1

| Method                  | Runtime            | Mean     | Ratio |
|------------------------ |------------------- |---------:|------:|
| HttpClientUnpatched     | .NET 8.0           | 133.7 ms |  1.00 |
| HttpClientUnpatched     | .NET Framework 4.8 | 130.4 ms |  0.98 |
|                         |                    |          |       |
| HttpClientPatched       | .NET 8.0           | 133.9 ms |  1.00 |
| HttpClientPatched       | .NET Framework 4.8 | 131.0 ms |  0.98 |
|                         |                    |          |       |
| HttpWebRequestUnpatched | .NET 8.0           | 277.5 ms |  1.00 |
| HttpWebRequestUnpatched | .NET Framework 4.8 |       NA |     ? |
|                         |                    |          |       |
| HttpWebRequestPatched   | .NET 8.0           | 283.5 ms |  1.00 |
| HttpWebRequestPatched   | .NET Framework 4.8 |       NA |     ? |
