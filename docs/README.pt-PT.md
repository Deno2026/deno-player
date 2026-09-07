# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | Português (Portugal) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**Um leitor multimédia simples para Windows, pensado para verificar rapidamente vídeos, áudio, imagens e vídeos com legendas guardados no computador.**

Sem anúncios. Sem conta. Sem sincronização na nuvem. Sem telemetria. Basta abrir um ficheiro e ver.

**Versão estável mais recente:** [v0.5.3](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.3) · Publicada em 2 de agosto de 2026

![Deno Video Player a reproduzir um vídeo de exemplo com legendas](assets/playback-preview.png)

*Reprodução real com legendas de um vídeo de exemplo criado pela DENO. A interface da aplicação está disponível em inglês e coreano. [Ecrã do primeiro arranque](assets/preview.png).*

## ✨ Porque é útil

- Reprodução rápida de ficheiros multimédia locais por arrastar e largar
- Cria automaticamente uma lista de reprodução simples com os ficheiros da mesma pasta
- Botões na barra superior e pegas finas no centro das margens para os ficheiros recentes e a lista de reprodução
- Capturas de ecrã com `Ctrl + S`
- Zoom de vídeo com `Ctrl + roda do rato` e deslocamento com arrasto do botão do meio
- Corte simples e sem perdas com `I` → `O` → `Ctrl + E`
- Um botão explícito de ecrã inteiro para mostrar apenas o vídeo
- Um guia rápido integrado e uma referência prática de atalhos com `F1`
- Interface da aplicação em inglês e coreano nas Definições
- As atualizações são apresentadas primeiro e só são instaladas quando escolher

## 🚀 Instalar em 3 passos

1. Transfira o instalador mais recente:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-win-Setup.exe)
2. Execute o instalador.
3. Abra o **Deno Video Player** e arraste um ficheiro multimédia para a janela.

No primeiro arranque, a aplicação prepara o motor de reprodução de que necessita. Normalmente, isto acontece apenas uma vez.

Se o Windows SmartScreen apresentar um aviso, confirme que o ficheiro veio da página oficial GitHub Releases e escolha **More info** → **Run anyway**.

### Requisitos do sistema

- Windows 10 ou Windows 11, x64
- Ligação à internet no primeiro arranque para preparar o motor de reprodução mpv
- Ligação à internet na primeira exportação de clip, áudio ou vídeo para preparar o FFmpeg

## ❓ Guia integrado

Clique no botão `?` ao lado de Settings ou prima `F1` a qualquer momento. O guia explica:

- onde estão os painéis dos ficheiros recentes e da lista de reprodução da pasta atual
- as principais ferramentas da barra superior e os controlos de reprodução
- os atalhos de teclado e rato
- legendas, faixas de áudio, capturas de ecrã, zoom e deslocamento
- a exportação de clip, apenas áudio e apenas vídeo
- a resolução de problemas do primeiro arranque e da reprodução

O leitor vazio também inclui uma ligação **New here?**, e as falhas de reprodução abrem diretamente a secção de resolução de problemas.

## 📦 Opção portátil

Se preferir não instalar, transfira [DenoVideoPlayer-v0.5.3-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-v0.5.3-portable-win-x64.zip), extraia o ficheiro e execute `DenoVideoPlayer.exe`.

Para a maioria dos principiantes, o instalador `Setup.exe` é a opção mais simples.

## 🎬 O que pode fazer

### Verificar multimédia rapidamente

Abra vídeos, áudio, imagens ou vídeos com legendas sem criar uma biblioteca pesada. O Deno Video Player é focado em verificar ficheiros locais com rapidez.

`F`, o duplo clique na área de visualização ou na barra de título e os botões de tamanho nos cantos superior e inferior direitos fazem o mesmo: uma janela normal passa a ecrã inteiro; uma janela em ecrã inteiro ou maximizada pelo Windows regressa ao tamanho normal anterior. Também funciona com o leitor vazio, durante o carregamento ou após uma falha de reprodução. Para abrir multimédia, utilize **Abrir ficheiro** / **Abrir pasta**, `Ctrl + O` ou arraste e largue.

### Explorar a mesma pasta

Ao abrir um ficheiro, a aplicação pode usar outros ficheiros multimédia da mesma pasta como uma lista de reprodução simples. Isto é útil para verificar renderizações, exportações, referências e clips transferidos.

Abra os ficheiros recentes pelo botão da barra superior ou com `Ctrl + H`. Abra a lista de reprodução da pasta atual pelo respetivo botão ou com `P` / `Ctrl + L`. Ordene por nome de ficheiro natural, mais recentes primeiro ou mais antigos primeiro; a ordem escolhida é memorizada e também controla a navegação para o ficheiro anterior ou seguinte.

Para uma consulta rápida, coloque o cursor sobre a pega fina no centro da margem esquerda para ver os ficheiros recentes, ou da margem direita para a lista de reprodução. Estes painéis fecham quando o cursor sai tanto da pega como do painel. Os painéis abertos por um botão ou atalho ficam abertos até os fechar ou mudar de painel.

Os controlos inferiores incluem repetição desativada, repetir tudo, repetir um item, aleatório, volume, velocidade de reprodução e ecrã inteiro. Clique no valor da velocidade para abrir os valores predefinidos ou use a roda do rato sobre ele para mudar em passos de 0,25x.

### Cortar sem recodificar

1. Prima `I` para marcar o início
2. Prima `O` para marcar o fim
3. Prima `Ctrl + E` para guardar o clip

O corte usa cópia de fluxo do FFmpeg, por isso é rápido e não recodifica o vídeo. O início e o fim podem ficar próximos do fotograma-chave mais próximo em vez de corresponderem exatamente ao fotograma visível.

No modo de edição, pode guardar o clip completo, extrair apenas o áudio ou apenas o vídeo. O FFmpeg é preparado a pedido na primeira exportação e a transferência pode ser grande.

## 🧩 Ficheiros suportados

- **Vídeo:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Áudio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Imagem:** `.jpg .jpeg .png .webp .bmp .gif`
- **Legendas:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 Idiomas

A interface da aplicação suporta atualmente:

- English
- Korean

Pode alterar o idioma de apresentação em **Settings**.

## ⌨️ Atalhos úteis

| Ação | Atalho |
| --- | --- |
| Reproduzir / pausar | `Space` |
| 2x enquanto mantém premido | Manter `Space` premido |
| Recuar / avançar 5 segundos | `←` / `→` |
| Recuar / avançar 30 segundos | `Shift + ←` / `Shift + →` |
| Volume | `↑` / `↓` ou roda do rato |
| Zoom / deslocamento do vídeo | `Ctrl + roda do rato` / arrastar com o botão do meio |
| Silenciar | `M` |
| Ecrã inteiro / restaurar janela | `F` / `F11` / `Enter` / `Alt + Enter` / duplo clique na área de visualização ou na barra de título |
| Sair do ecrã inteiro | `Esc` |
| Ficheiro anterior / seguinte | `PageUp` / `PageDown` ou `Ctrl + ←` / `Ctrl + →` |
| Captura de ecrã | `Ctrl + S` |
| Sempre visível | `Ctrl + T` |
| Ficheiros recentes | `Ctrl + H` |
| Lista de reprodução | `P` / `Ctrl + L` |
| Faixa de legendas | `V` / `Shift + V` |
| Faixa de áudio | `Ctrl + J` |
| Cortar clip | `I` → `O` → `Ctrl + E` |
| Guia e atalhos | `F1` |

## 🔒 O que não faz

O Deno Video Player evita intencionalmente as funcionalidades pesadas de uma biblioteca multimédia.

Não inclui anúncios, início de sessão, sincronização na nuvem, análises, recomendações, indexação em segundo plano, loja, mercado de extensões, editor de cronologia ou funcionalidades de IA.

## 🗒️ Atualizações

Consulte [CHANGELOG.md](../CHANGELOG.md) para ver as alterações recentes.

## 🛠️ Notas para programadores

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 Licença

O código-fonte do Deno Video Player é disponibilizado sob a [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`). Pode usá-lo, estudá-lo, modificá-lo e redistribuí-lo, incluindo para fins comerciais. As versões modificadas que distribuir têm de cumprir a GPL-3.0 e manter os avisos de licença e direitos de autor exigidos.

As ferramentas de terceiros, como mpv, FFmpeg, Velopack e 7-Zip, mantêm as respetivas licenças. Consulte [NOTICE.md](../NOTICE.md) para mais informações.
