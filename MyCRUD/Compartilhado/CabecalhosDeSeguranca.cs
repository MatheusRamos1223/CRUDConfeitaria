namespace MyCRUD.Compartilhado;

/// <summary>
/// Middleware que acrescenta cabeçalhos de segurança a <b>toda</b> resposta (R43).
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Cabeçalho</term><description>Ataque que evita</description></listheader>
///   <item><term><c>X-Content-Type-Options: nosniff</c></term><description>Navegador "adivinhar" o tipo e executar como script um arquivo que não é script.</description></item>
///   <item><term><c>Referrer-Policy: strict-origin-when-cross-origin</c></term><description>Vazar caminho e query string (ex.: <c>?busca=maria</c>) para outros sites.</description></item>
///   <item><term><c>X-Frame-Options: DENY</c></term><description>Clickjacking: outra página exibir a nossa num <c>&lt;iframe&gt;</c> invisível.</description></item>
///   <item><term><c>Content-Security-Policy</c></term><description>XSS: só executa script e CSS servidos por esta origem; nada inline.</description></item>
/// </list>
/// <para>
/// <b>Exceção:</b> <c>/scalar</c> e <c>/openapi</c> (só existem em Development) ficam sem CSP,
/// porque a interface do Scalar carrega script de CDN e usa estilo inline.
/// Os demais cabeçalhos valem para elas também.
/// </para>
/// <para>
/// <b>Por que <c>OnStarting</c>?</b> O <c>UseExceptionHandler</c> limpa os cabeçalhos da
/// resposta antes de escrever o Problem Details de um erro. Gravando os cabeçalhos no
/// último instante (quando a resposta começa a ser enviada), eles saem inclusive nas
/// respostas de erro.
/// </para>
/// </remarks>
public static class CabecalhosDeSeguranca
{
    /// <summary>
    /// Política de conteúdo das telas. As páginas usam só arquivos de <c>/css</c> e <c>/js</c>
    /// (sem <c>&lt;script&gt;</c> inline, atributos <c>on*</c> ou <c>style=</c>).
    /// </summary>
    public const string PoliticaDeConteudo =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>Registra o middleware. Deve ser o primeiro do pipeline.</summary>
    /// <param name="app">Aplicação.</param>
    /// <returns>A mesma aplicação, para encadear chamadas.</returns>
    public static IApplicationBuilder UseCabecalhosDeSeguranca(this IApplicationBuilder app) =>
        app.Use(async (contexto, proximo) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var cabecalhos = contexto.Response.Headers;
                cabecalhos["X-Content-Type-Options"] = "nosniff";
                cabecalhos["Referrer-Policy"] = "strict-origin-when-cross-origin";
                cabecalhos["X-Frame-Options"] = "DENY";

                if (!EhDocumentacaoDaApi(contexto.Request.Path))
                {
                    cabecalhos["Content-Security-Policy"] = PoliticaDeConteudo;
                }

                return Task.CompletedTask;
            });

            await proximo(contexto);
        });

    /// <summary>Rotas do Scalar e do documento OpenAPI, que precisam de CDN e estilo inline.</summary>
    private static bool EhDocumentacaoDaApi(PathString caminho) =>
        caminho.StartsWithSegments("/scalar") || caminho.StartsWithSegments("/openapi");
}
