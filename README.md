# Palavra Secreta

Aplicativo em .NET MAUI para gerar e jogar desafios de palavra secreta, com modos de geracao e jogo organizados em abas.

## O que existe hoje

- Aba para gerar desafios
- Aba para jogar
- Banco local de palavras padrao
- Servicos para geracao de palavras e desenho do tabuleiro

## Estrutura principal

- `Pages/GeneratorPage.xaml`: fluxo de geracao
- `Pages/GamePage.xaml`: fluxo principal do jogo
- `Services`: integracoes de geracao e banco local
- `Resources/Raw/default_word_bank.json`: base padrao de palavras

## Como executar

1. Abra `PalavraSecreta/PalavraSecreta.sln` no Visual Studio 2022.
2. Instale o workload do .NET MAUI.
3. Restaure os pacotes e rode na plataforma desejada.

## Observacao

Se houver integracao com IA ou API externa, configure as chaves apenas no ambiente local e nunca no README.