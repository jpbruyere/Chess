Chess
=====
Stockfish client using my libraries.

###Building from sources

```
git clone https://github.com/jpbruyere/Chess.git   	# Download sources
cd Chess
git submodule update --init --recursive             # Get submodules
xbuild  /p:Configuration=Release Chess.sln       # Build
```
The resulting executable will be in **build/Release**.

Please report bugs and issues on [GitHub](https://github.com/jpbruyere/Chess/issues)

####Screen shots :
<table width="100%">
  <tr>
    <td width="30%" align="center"><img src="/screenshot.png?raw=true" alt="chess" width="90%"/></td>
    <td width="30%" align="center"><img src="/screenshot2.png?raw=true" alt="chess" width="90%" /> </td>
    <td width="30%" align="center"><img src="/screenshot4.png?raw=true" alt="chess" width="90%"/> </td>
  </tr>
</table>
