# Chess
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.me/GrandTetraSoftware) [![Build Status](https://travis-ci.org/jpbruyere/Chess.svg?branch=master)](https://travis-ci.org/jpbruyere/Chess) [![Build Status Windows](https://ci.appveyor.com/api/projects/status/j387lo59vnov8jbc?svg=true)](https://ci.appveyor.com/project/jpbruyere/Chess)

[Stockfish](https://stockfishchess.org/) client using **Crow.OpenTK** libraries.

Please report bugs and issues on [GitHub](https://github.com/jpbruyere/Chess/issues)

### Building
```bash
git clone https://github.com/jpbruyere/Chess.git  # Download sources
cd Chess
git submodule update --init --recursive           # Get submodules
nuget restore Chess.sln                           # restore nuget
xbuild  /p:Configuration=Release Chess.sln        # Compile
```
The resulting executable will be in **build/Release**.

### Screen shots :
<table width="100%">
  <tr>
    <td width="30%" align="center"><img src="/screenshot.png?raw=true" alt="chess" width="90%"/></td>
    <td width="30%" align="center"><img src="/screenshot2.png?raw=true" alt="chess" width="90%" /> </td>
    <td width="30%" align="center"><img src="/screenshot4.png?raw=true" alt="chess" width="90%"/> </td>
  </tr>
</table>
