### Code coverage (unit and integration tests)

| Visibility | Missed | Gap | Line | Branch | Method (called) |
| --- | ---: | ---: | ---: | ---: | ---: |
| &nbsp;&nbsp;Internal | 11 | 57.9% | 21.4% (3/14) | 25.0% (1/4) | - |
| &nbsp;&nbsp;Public API | 3 | 15.8% | 72.7% (8/11) | 25.0% (1/4) | 66.7% (2/3) |
| **Alpha (assembly)** | **14** | **73.7%** | **44.0% (11/25)** | **25.0% (2/8)** | **66.7% (2/3)** |
| &nbsp;&nbsp;Unknown (no PublicAPI files) | 5 | 26.3% | 37.5% (3/8) | - | - |
| **Beta (assembly)** | **5** | **26.3%** | **37.5% (3/8)** | **-** | **-** |
| **Total** | **19** | **100.0%** | **42.4% (14/33)** | **25.0% (2/8)** | **66.7% (2/3)** |

<details><summary>Coverage by namespace</summary>

| Namespace | Missed | Gap | Line | Branch | Method (called) |
| --- | ---: | ---: | ---: | ---: | ---: |
| &nbsp;&nbsp;Alpha.Internal | 9 | 47.4% | 25.0% (3/12) | 25.0% (1/4) | - |
| &nbsp;&nbsp;Alpha.Api | 3 | 15.8% | 66.7% (6/9) | 25.0% (1/4) | 66.7% (2/3) |
| &nbsp;&nbsp;Alpha | 2 | 10.5% | 50.0% (2/4) | - | - |
| **Alpha (assembly)** | **14** | **73.7%** | **44.0% (11/25)** | **25.0% (2/8)** | **66.7% (2/3)** |
| &nbsp;&nbsp;Beta | 5 | 26.3% | 16.7% (1/6) | - | - |
| &nbsp;&nbsp;Beta.Tools | 0 | 0.0% | 100.0% (2/2) | - | - |
| **Beta (assembly)** | **5** | **26.3%** | **37.5% (3/8)** | **-** | **-** |
| **Total** | **19** | **100.0%** | **42.4% (14/33)** | **25.0% (2/8)** | **66.7% (2/3)** |

</details>

<details><summary>Coverage by area (top-level folder)</summary>

| Area | Missed | Gap | Line | Branch | Method (called) |
| --- | ---: | ---: | ---: | ---: | ---: |
| &nbsp;&nbsp;Internal | 9 | 47.4% | 25.0% (3/12) | 25.0% (1/4) | - |
| &nbsp;&nbsp;Api | 3 | 15.8% | 66.7% (6/9) | 25.0% (1/4) | 66.7% (2/3) |
| &nbsp;&nbsp;(root) | 2 | 10.5% | 50.0% (2/4) | - | - |
| **Alpha (assembly)** | **14** | **73.7%** | **44.0% (11/25)** | **25.0% (2/8)** | **66.7% (2/3)** |
| &nbsp;&nbsp;(root) | 5 | 26.3% | 16.7% (1/6) | - | - |
| &nbsp;&nbsp;Tools | 0 | 0.0% | 100.0% (2/2) | - | - |
| **Beta (assembly)** | **5** | **26.3%** | **37.5% (3/8)** | **-** | **-** |
| **Total** | **19** | **100.0%** | **42.4% (14/33)** | **25.0% (2/8)** | **66.7% (2/3)** |

</details>

<details><summary>Top 10 classes by missed lines</summary>

| Class | Missed | Gap | Line | File |
| --- | ---: | ---: | ---: | --- |
| `Beta.Thing` | 5 | 26.3% | 16.7% (1/6) | Thing.cs |
| `Alpha.Internal.Parser` | 3 | 15.8% | 0.0% (0/3) | Internal/Parser.Tokens.cs |
| `Alpha.Internal.Cache<TKey, TValue>` | 3 | 15.8% | 25.0% (1/4) | Internal/Cache.cs |
| `Alpha.Internal.Parser` | 3 | 15.8% | 40.0% (2/5) | Internal/Parser.cs |
| `Alpha.Api.Widget` | 2 | 10.5% | 50.0% (2/4) | Api/Widget.cs |
| `Alpha.Generated` | 2 | 10.5% | 0.0% (0/2) | Generated.cs |
| `Alpha.Api.Widget.Inner` | 1 | 5.3% | 50.0% (1/2) | Api/Widget.cs |

</details>

Sorted by missed lines. Gap: share of all missed lines. Method (called): methods entered at least once.

