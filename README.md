Chess   [![Build Status](https://travis-ci.org/jpbruyere/Chess.svg?branch=master)](https://travis-ci.org/jpbruyere/Chess) [![Build Status Windows](https://ci.appveyor.com/api/projects/status/j387lo59vnov8jbc?svg=true)](https://ci.appveyor.com/project/jpbruyere/Chess)
=====
[Stockfish](https://stockfishchess.org/) client using **Crow.OpenTK** libraries.

Please report bugs and issues on [GitHub](https://github.com/jpbruyere/Chess/issues)

###Building from sources
```bash
git clone https://github.com/jpbruyere/Chess.git   	# Download sources
cd Chess
git submodule update --init --recursive             # Get submodules
nuget restore										# restore nuget
xbuild  /p:Configuration=Release Chess.sln          # Compile
```
The resulting executable will be in **build/Release**.

###Running
On the first startup, you need to provide the **stockish** executable path.
Go to the **options** menu, and enter the full path. As soon as the file is found, a green
light will inform you that stockfish is running.

On Debian, the path is `/usr/games/stockfish`.

###Screen shots :
<table width="100%">
  <tr>
    <td width="30%" align="center"><img src="/screenshot.png?raw=true" alt="chess" width="90%"/></td>
    <td width="30%" align="center"><img src="/screenshot2.png?raw=true" alt="chess" width="90%" /> </td>
    <td width="30%" align="center"><img src="/screenshot4.png?raw=true" alt="chess" width="90%"/> </td>
  </tr>
</table>
