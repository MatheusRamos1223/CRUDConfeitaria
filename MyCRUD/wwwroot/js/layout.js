// Layout comum das telas internas: cabeçalho com menu, usuário logado e botão Sair,
// mais funções pequenas de exibição usadas por todas as páginas.
//
// Ordem dos scripts em cada página:
//   <script src="/js/api.js"></script>               (api, exigirLogin, mensagemDeErro)
//   <script src="/js/layout.js"></script>
//   <script src="/js/paginas/<pagina>.js"></script>  (código da página)
//
// Nada de <script> inline, atributos on* ou style= nas páginas: a Content-Security-Policy
// enviada pelo servidor (script-src 'self'; style-src 'self') bloqueia tudo isso.
// Estilo muda por classe (classList), nunca por element.style.
//
// Uso típico:
//   iniciarLayout({ ativo: "produtos" }).then((usuario) => {
//       if (!usuario) return;          // sem sessão: já foi redirecionado ao login
//       carregarLista();
//   });
//
// Regra de segurança: todo texto vindo da API entra na página por textContent,
// nunca por innerHTML. As funções abaixo seguem essa regra.

/** Itens do menu, na ordem de exibição. "pagina" é o nome do arquivo .html. */
const ITENS_DO_MENU = [
    { pagina: "pedidos", rotulo: "Pedidos" },
    { pagina: "clientes", rotulo: "Clientes" },
    { pagina: "produtos", rotulo: "Produtos" },
    { pagina: "categorias", rotulo: "Categorias" }
];

/**
 * Monta o cabeçalho e confere a sessão.
 *
 * @param {object} opcoes
 * @param {string} opcoes.ativo      Página atual (destaca o item do menu).
 * @param {object} [opcoes.usuario]  SOMENTE protótipos: usuário fictício; não chama a API.
 * @param {boolean} [opcoes.prototipo] SOMENTE protótipos: mostra a faixa "PROTÓTIPO"
 *                                   e usa links relativos entre os protótipos.
 * @returns {Promise<{id:number,nome:string,email:string,papel:string}|null>}
 *          O usuário logado, ou null quando não há sessão (a página já foi redirecionada).
 */
async function iniciarLayout({ ativo, usuario = null, prototipo = false } = {}) {
    if (!usuario) {
        usuario = await exigirLogin();
        if (!usuario) return null;
    }

    // data-papel no <body> permite ao CSS esconder botões .so-admin.
    // Esconder é só conforto: a API recusa com 403 quem não tem permissão.
    document.body.dataset.papel = usuario.papel ?? "Operador";

    const topo = criarCabecalho(ativo, usuario, prototipo);
    document.body.prepend(topo);

    if (prototipo) {
        const faixa = document.createElement("div");
        faixa.className = "banner-prototipo";
        faixa.textContent = "PROTÓTIPO — dados fictícios, nenhuma chamada à API";
        document.body.prepend(faixa);
    }

    return usuario;
}

function criarCabecalho(ativo, usuario, prototipo) {
    const topo = document.createElement("header");
    topo.className = "topo";

    const interno = document.createElement("div");
    interno.className = "topo-interno";

    const marca = document.createElement("a");
    marca.className = "marca";
    marca.href = prototipo ? "pedidos.html" : "/pedidos.html";
    marca.textContent = "Confeitaria";

    const menu = document.createElement("nav");
    menu.className = "menu";
    menu.setAttribute("aria-label", "Menu principal");

    for (const item of ITENS_DO_MENU) {
        const link = document.createElement("a");
        link.href = prototipo ? `${item.pagina}.html` : `/${item.pagina}.html`;
        link.textContent = item.rotulo;
        if (item.pagina === ativo) link.setAttribute("aria-current", "page");
        menu.appendChild(link);
    }

    const areaUsuario = document.createElement("div");
    areaUsuario.className = "usuario";

    const nome = document.createElement("span");
    nome.className = "usuario-nome";
    nome.textContent = usuario.nome;

    const papel = document.createElement("span");
    papel.className = usuario.papel === "Admin" ? "papel papel-admin" : "papel";
    papel.textContent = usuario.papel ?? "Operador";

    const sair = document.createElement("button");
    sair.type = "button";
    sair.className = "botao botao-pequeno botao-sair";
    sair.textContent = "Sair";
    sair.addEventListener("click", () => sairDaSessao(sair, prototipo));

    areaUsuario.append(nome, papel, sair);
    interno.append(marca, menu, areaUsuario);
    topo.appendChild(interno);
    return topo;
}

async function sairDaSessao(botao, prototipo) {
    if (prototipo) {
        window.location.href = "../login.html";
        return;
    }

    botao.disabled = true;

    try {
        const resposta = await api("/login/sair", { method: "POST" });

        // 401 = sessão já tinha expirado; para o usuário o efeito é o mesmo.
        if (resposta.ok || resposta.status === 401) {
            irParaLogin();
            return;
        }

        alert("Não foi possível sair. Tente novamente.");
    } catch {
        alert("Não foi possível conectar ao servidor.");
    } finally {
        botao.disabled = false;
    }
}

// ---------------------------------------------------------------------------
// Formatação
// ---------------------------------------------------------------------------

/** 45.9 → "R$ 45,90" */
function formatarMoeda(valor) {
    return Number(valor).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

/**
 * "2026-11-20" (DateOnly da API) → "20/11/2026".
 * Não usa new Date(): ela interpretaria a data como UTC e poderia mostrar o dia anterior.
 */
function formatarData(dataIso) {
    if (!dataIso) return "";
    const [ano, mes, dia] = dataIso.slice(0, 10).split("-");
    return `${dia}/${mes}/${ano}`;
}

/** "2026-11-17T14:05:00Z" (UTC) → "17/11/2026 11:05" no fuso do navegador. */
function formatarDataHora(dataHoraIso) {
    if (!dataHoraIso) return "";
    return new Date(dataHoraIso).toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}

/** "19999990000" → "(19) 99999-0000"; "1934567890" → "(19) 3456-7890". */
function formatarTelefone(digitos) {
    const d = String(digitos ?? "");
    if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
    if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
    return d;
}

// ---------------------------------------------------------------------------
// Construção de elementos (sempre via textContent)
// ---------------------------------------------------------------------------

/** Cria uma <td> com texto e classe opcional. */
function criarCelula(texto, classe) {
    const celula = document.createElement("td");
    celula.textContent = texto ?? "";
    if (classe) celula.className = classe;
    return celula;
}

/**
 * Cria um botão.
 * @param {string} texto
 * @param {string} [classe]  ex.: "botao-perigo so-admin"
 * @param {Function} [aoClicar]
 */
function criarBotao(texto, classe, aoClicar) {
    const botao = document.createElement("button");
    botao.type = "button";
    botao.className = `botao botao-pequeno ${classe ?? ""}`.trim();
    botao.textContent = texto;
    if (aoClicar) botao.addEventListener("click", aoClicar);
    return botao;
}

/**
 * Selo colorido de situação/status.
 * Classes previstas em app.css: selo-ativo, selo-inativo, selo-Aberto,
 * selo-Confirmado, selo-Entregue, selo-Cancelado.
 */
function criarSelo(texto, chave) {
    const selo = document.createElement("span");
    selo.className = `selo selo-${chave ?? texto}`;
    selo.textContent = texto;
    return selo;
}

/**
 * Mostra uma mensagem no elemento .mensagem da página.
 * @param {HTMLElement} elemento
 * @param {string} texto  Vazio limpa a mensagem.
 * @param {"sucesso"|"erro"|"info"} [tipo]
 */
function mostrarMensagem(elemento, texto, tipo = "info") {
    elemento.textContent = texto;
    elemento.className = texto ? `mensagem alerta alerta-${tipo}` : "mensagem";
}

// ---------------------------------------------------------------------------
// Paginação
// ---------------------------------------------------------------------------

/**
 * Desenha o paginador a partir do envelope Pagina<T> devolvido pela API:
 *   { itens: [...], pagina: 2, tamanhoPagina: 20, total: 57, totalPaginas: 3 }
 *
 * @param {HTMLElement} elemento  Container .paginador
 * @param {{pagina:number,tamanhoPagina:number,total:number,totalPaginas:number}} pagina
 * @param {(novaPagina:number) => void} aoMudar
 */
function montarPaginador(elemento, { pagina, tamanhoPagina, total, totalPaginas }, aoMudar) {
    elemento.replaceChildren();

    const resumo = document.createElement("span");
    if (total === 0) {
        resumo.textContent = "Nenhum registro";
    } else {
        const inicio = (pagina - 1) * tamanhoPagina + 1;
        const fim = Math.min(pagina * tamanhoPagina, total);
        resumo.textContent = `${inicio}–${fim} de ${total}`;
    }

    const paginas = document.createElement("div");
    paginas.className = "paginas";

    const anterior = criarBotao("‹", "", () => aoMudar(pagina - 1));
    anterior.disabled = pagina <= 1;
    anterior.setAttribute("aria-label", "Página anterior");
    paginas.appendChild(anterior);

    // Mostra no máximo 5 números ao redor da página atual.
    const primeira = Math.max(1, Math.min(pagina - 2, totalPaginas - 4));
    const ultima = Math.min(totalPaginas, primeira + 4);

    for (let n = primeira; n <= ultima; n++) {
        const botao = criarBotao(String(n), "", () => aoMudar(n));
        if (n === pagina) botao.setAttribute("aria-current", "page");
        paginas.appendChild(botao);
    }

    const proxima = criarBotao("›", "", () => aoMudar(pagina + 1));
    proxima.disabled = pagina >= totalPaginas;
    proxima.setAttribute("aria-label", "Próxima página");
    paginas.appendChild(proxima);

    elemento.append(resumo, paginas);
}

/**
 * Monta a query string de uma listagem ignorando campos vazios.
 * montarQuery({ nome: "bolo", categoriaId: "", pagina: 2 }) → "?nome=bolo&pagina=2"
 */
function montarQuery(parametros) {
    const query = new URLSearchParams();
    for (const [chave, valor] of Object.entries(parametros)) {
        if (valor !== "" && valor !== null && valor !== undefined) query.set(chave, valor);
    }
    const texto = query.toString();
    return texto ? `?${texto}` : "";
}
