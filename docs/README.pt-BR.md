# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | Português (Brasil) | [Bahasa Indonesia](README.id.md)

**Um player de mídia limpo para Windows, feito para revisar rapidamente vídeos, áudios, imagens e vídeos com legendas.**

Sem anúncios. Sem conta. Sem sincronização em nuvem. Sem telemetria. Abra um arquivo e assista.

![Deno Video Player preview](assets/preview.png)

## ✨ Por que ele é útil

- Reproduz mídia local com arrastar e soltar
- Cria automaticamente uma lista simples com arquivos da mesma pasta
- Painéis laterais por hover para playlist e arquivos recentes
- Capturas de tela com `Ctrl + S`
- Zoom do vídeo com `Ctrl + roda do mouse` e movimento arrastando o botão do meio
- Corte simples e sem perda com `I` → `O` → `Ctrl + E`
- Um botão de tela cheia para deixar somente o vídeo visível
- Interface do app em inglês e coreano nas configurações
- Atualizações aparecem primeiro e só são instaladas quando você escolhe

## 🚀 Instale em 3 passos

1. Baixe o instalador mais recente:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/latest/download/DenoVideoPlayer-win-Setup.exe)
2. Execute o instalador.
3. Abra o **Deno Video Player** e arraste um arquivo de mídia para a janela.

Na primeira abertura, o app prepara o mecanismo de reprodução necessário. Normalmente isso acontece apenas uma vez.

Se o Windows SmartScreen aparecer, confirme que o arquivo veio da página oficial do GitHub Releases e escolha **More info** → **Run anyway**.

## 📦 Opção portátil

Se você prefere não instalar, baixe o `portable-win-x64.zip` mais recente em [Releases](https://github.com/Deno2026/deno-video-player/releases), extraia o arquivo e execute `DenoVideoPlayer.exe`.

Para a maioria dos iniciantes, o `Setup.exe` é a opção mais fácil.

## 🎬 O que você pode fazer

### Revisar mídia rapidamente

Abra vídeos, áudio, imagens ou vídeos com legenda sem criar uma biblioteca pesada. O Deno Video Player é focado em revisar arquivos locais com rapidez.

### Navegar pela mesma pasta

Ao abrir um arquivo, o app pode usar outros arquivos de mídia da mesma pasta como uma playlist simples. Isso ajuda a revisar renders, exportações, referências e clipes baixados.

### Cortar sem recodificar

1. Pressione `I` para marcar o início
2. Pressione `O` para marcar o fim
3. Pressione `Ctrl + E` para salvar o clipe

O corte usa FFmpeg stream copy, então é rápido e não recodifica o vídeo. O início e o fim podem ficar próximos ao keyframe mais próximo.

No modo de edição, você pode salvar o clipe completo, extrair apenas o áudio ou apenas o vídeo.
O FFmpeg é preparado sob demanda na primeira exportação, e o download pode ser grande.

## 🧩 Arquivos compatíveis

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 Idiomas

A interface do app atualmente oferece:

- English
- Korean

Você pode mudar o idioma em **Settings**.

## ⌨️ Atalhos úteis

| Ação | Atalho |
| --- | --- |
| Reproduzir / pausar | `Space` |
| 2x enquanto segura | Segurar `Space` |
| Avançar / voltar 5 segundos | `←` / `→` |
| Avançar / voltar 30 segundos | `Shift + ←` / `Shift + →` |
| Volume | `↑` / `↓` ou roda do mouse |
| Zoom / movimento do vídeo | `Ctrl + roda do mouse` / arrastar com botão do meio |
| Mudo | `M` |
| Tela cheia | `F` / `F11` / `Enter` / `Alt + Enter` / duplo clique |
| Sair da tela cheia | `Esc` |
| Arquivo anterior / próximo | `PageUp` / `PageDown` ou `Ctrl + ←` / `Ctrl + →` |
| Captura de tela | `Ctrl + S` |
| Sempre no topo | `Ctrl + T` |
| Playlist | `P` / `Ctrl + L` |
| Faixa de legenda | `V` / `Shift + V` |
| Faixa de áudio | `Ctrl + J` |
| Cortar clipe | `I` → `O` → `Ctrl + E` |

## 🔒 O que ele não faz

O Deno Video Player evita de propósito funções pesadas de biblioteca de mídia.

Ele não inclui anúncios, login, sincronização em nuvem, analytics, recomendações, indexação em segundo plano, loja, marketplace de plugins, editor de timeline ou recursos de IA.

## 🗒️ Atualizações

Veja [CHANGELOG.md](../CHANGELOG.md) para mudanças recentes.

## 🛠️ Para desenvolvedores

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 Licença

O código-fonte do Deno Video Player é lançado sob [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`). Você pode usar, estudar, modificar e redistribuir, inclusive comercialmente. Versões modificadas distribuídas devem seguir a GPL-3.0 e manter os avisos de licença e copyright exigidos.

Ferramentas de terceiros como mpv, FFmpeg, Velopack e 7-Zip continuam sob suas próprias licenças. Veja [NOTICE.md](../NOTICE.md) para detalhes.
