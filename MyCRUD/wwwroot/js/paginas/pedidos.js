// Código da página pedidos.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

// Limites iguais aos da API (Pedido.ItensMaximo, ItemPedido.QuantidadeMaxima).
// A API valida de novo: aqui é só para o usuário não perder tempo.
const ITENS_MAXIMO = 50;
const QUANTIDADE_MAXIMA = 999;
const TAMANHO_PAGINA = 20;

const mensagem = document.querySelector("#mensagem");
const formFiltro = document.querySelector("#form-filtro");
const linhas = document.querySelector("#linhas");
const contagem = document.querySelector("#contagem");
const paginador = document.querySelector("#paginador");

const formPedido = document.querySelector("#form-pedido");
const selectCliente = document.querySelector("#clienteId");
const inputData = document.querySelector("#dataEntrega");
const containerItens = document.querySelector("#itens");
const botaoAdicionar = document.querySelector("#adicionar-item");
const totalPrevisto = document.querySelector("#total-previsto");
const botaoCriar = document.querySelector("#botao-criar");

/** Produtos ativos do cardápio: [{ id, nome, preco }]. Preenchido em carregarOpcoes. */
let produtosAtivos = [];

/**
 * "Hoje" no fuso da confeitaria, no formato AAAA-MM-DD (o do <input type="date"> e da API).
 * O locale "en-CA" é usado só porque formata a data exatamente assim.
 * Mesmo fuso da API (R21): um computador com relógio em outro fuso não muda a regra.
 */
function hojeNaConfeitaria() {
    return new Intl.DateTimeFormat("en-CA", {
        timeZone: "America/Sao_Paulo", year: "numeric", month: "2-digit", day: "2-digit"
    }).format(new Date());
}

// -------------------------------------------------------------------
// Lista
// -------------------------------------------------------------------

async function carregarLista(pagina = 1) {
    const filtro = new FormData(formFiltro);
    const query = montarQuery({
        busca: filtro.get("busca").trim(),
        status: filtro.get("status"),
        de: filtro.get("de"),
        ate: filtro.get("ate"),
        pagina,
        tamanho: TAMANHO_PAGINA
    });

    try {
        const resposta = await api(`/pedidos/${query}`);

        if (!resposta.ok) {
            mostrarMensagem(mensagem,
                await mensagemDeErro(resposta, "Não foi possível carregar os pedidos."), "erro");
            return;
        }

        desenharLista(await resposta.json());
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
    }
}

/** Desenha as linhas a partir do envelope ResultadoPaginado<PedidoResumoDto>. */
function desenharLista(resultado) {
    linhas.replaceChildren();

    if (resultado.itens.length === 0) {
        const tr = document.createElement("tr");
        const vazio = criarCelula("Nenhum pedido encontrado.", "lista-vazia");
        vazio.colSpan = 7;
        tr.appendChild(vazio);
        linhas.appendChild(tr);
    }

    for (const pedido of resultado.itens) {
        const tr = document.createElement("tr");

        const status = document.createElement("td");
        status.appendChild(criarSelo(pedido.status));

        const acoes = document.createElement("td");
        acoes.className = "acoes";
        const abrir = document.createElement("a");
        abrir.className = "botao botao-pequeno";
        abrir.href = `/pedido-detalhe.html?id=${pedido.id}`;
        abrir.textContent = "Abrir";
        abrir.setAttribute("aria-label", `Abrir pedido ${pedido.id}`);
        acoes.appendChild(abrir);

        tr.append(
            criarCelula(`#${pedido.id}`, "id"),
            criarCelula(pedido.clienteNome),
            criarCelula(formatarData(pedido.dataEntrega)),
            criarCelula(String(pedido.quantidadeItens), "num"),
            criarCelula(formatarMoeda(pedido.total), "num"),
            status,
            acoes);
        linhas.appendChild(tr);
    }

    contagem.textContent = `${resultado.total} pedido(s)`;
    montarPaginador(paginador, resultado, carregarLista);
}

formFiltro.addEventListener("submit", (evento) => {
    evento.preventDefault();
    mostrarMensagem(mensagem, "");
    carregarLista(1);
});

// O evento "reset" dispara ANTES de o navegador limpar os campos;
// setTimeout espera a limpeza para então recarregar sem filtros.
formFiltro.addEventListener("reset", () => {
    mostrarMensagem(mensagem, "");
    setTimeout(() => carregarLista(1));
});

// -------------------------------------------------------------------
// Formulário "Novo pedido"
// -------------------------------------------------------------------

/** Carrega os selects: clientes e produtos ativos (até 100 de cada, limite aceito no contrato). */
async function carregarOpcoes() {
    try {
        const [respostaClientes, respostaProdutos] = await Promise.all([
            api("/clientes/?tamanho=100"),
            api("/produtos/?situacao=Ativos&tamanho=100")
        ]);

        if (!respostaClientes.ok) {
            mostrarMensagem(mensagem,
                await mensagemDeErro(respostaClientes, "Não foi possível carregar os clientes."), "erro");
            return;
        }
        if (!respostaProdutos.ok) {
            mostrarMensagem(mensagem,
                await mensagemDeErro(respostaProdutos, "Não foi possível carregar os produtos."), "erro");
            return;
        }

        const clientes = (await respostaClientes.json()).itens;
        produtosAtivos = (await respostaProdutos.json()).itens;

        selectCliente.replaceChildren(new Option("Selecione…", ""));
        for (const cliente of clientes) {
            selectCliente.appendChild(new Option(cliente.nome, cliente.id));
        }

        prepararFormulario();

        if (clientes.length === 0) {
            mostrarMensagem(mensagem, "Nenhum cliente cadastrado. Cadastre em Clientes antes de criar pedidos.", "info");
        } else if (produtosAtivos.length === 0) {
            mostrarMensagem(mensagem, "Nenhum produto ativo. Cadastre ou reative em Produtos.", "info");
        }
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
    }
}

/** Estado inicial do formulário: uma linha de item vazia e data mínima = hoje. */
function prepararFormulario() {
    inputData.min = hojeNaConfeitaria();
    containerItens.replaceChildren();
    adicionarItem();
}

/** Acrescenta uma linha "produto + quantidade + remover". */
function adicionarItem() {
    if (containerItens.children.length >= ITENS_MAXIMO) return;

    const linha = document.createElement("div");
    linha.className = "item-formulario";

    const produto = document.createElement("select");
    produto.required = true;
    produto.setAttribute("aria-label", "Produto");
    produto.appendChild(new Option("Selecione o produto…", ""));
    for (const p of produtosAtivos) {
        produto.appendChild(new Option(`${p.nome} — ${formatarMoeda(p.preco)}`, p.id));
    }

    const quantidade = document.createElement("input");
    quantidade.type = "number";
    quantidade.min = "1";
    quantidade.max = String(QUANTIDADE_MAXIMA);
    quantidade.step = "1";
    quantidade.value = "1";
    quantidade.required = true;
    quantidade.setAttribute("aria-label", "Quantidade");

    const remover = criarBotao("✕", "botao-icone botao-perigo", () => {
        linha.remove();
        // Sempre deixa ao menos uma linha para o usuário preencher.
        if (containerItens.children.length === 0) adicionarItem();
        atualizarFormulario();
    });
    remover.setAttribute("aria-label", "Remover item");

    produto.addEventListener("change", atualizarFormulario);
    quantidade.addEventListener("input", atualizarFormulario);

    linha.append(produto, quantidade, remover);
    containerItens.appendChild(linha);
    atualizarFormulario();
}

/** Lê as linhas do formulário no formato da API: [{ produtoId, quantidade }]. */
function lerItens() {
    return [...containerItens.querySelectorAll(".item-formulario")].map((linha) => ({
        produtoId: Number(linha.querySelector("select").value),
        quantidade: Number(linha.querySelector("input").value)
    }));
}

/** Recalcula o total estimado (preço ATUAL) e o limite do botão "+ Adicionar item". */
function atualizarFormulario() {
    let total = 0;
    for (const item of lerItens()) {
        const produto = produtosAtivos.find((p) => p.id === item.produtoId);
        if (produto && Number.isInteger(item.quantidade) && item.quantidade > 0) {
            total += produto.preco * item.quantidade;
        }
    }
    totalPrevisto.textContent = formatarMoeda(total);
    botaoAdicionar.disabled = containerItens.children.length >= ITENS_MAXIMO;
}

botaoAdicionar.addEventListener("click", adicionarItem);

formPedido.addEventListener("submit", async (evento) => {
    evento.preventDefault();
    mostrarMensagem(mensagem, "");

    // Campos obrigatórios, min e max já foram conferidos pelo navegador
    // (required/min/max). Falta só a regra "sem produto repetido".
    const itens = lerItens();
    const ids = itens.map((i) => i.produtoId);
    if (new Set(ids).size !== ids.length) {
        mostrarMensagem(mensagem, "Há produto repetido. Use uma linha só e some as quantidades.", "erro");
        return;
    }

    // Note: nenhum preço é enviado. A API lê o preço do cadastro (R23).
    const corpo = {
        clienteId: Number(selectCliente.value),
        dataEntrega: inputData.value,
        itens
    };

    botaoCriar.disabled = true;

    try {
        const resposta = await api("/pedidos/", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(corpo)
        });

        if (resposta.ok) {
            // Sucesso → abre o detalhe do pedido criado (T4).
            const pedido = await resposta.json();
            window.location.href = `/pedido-detalhe.html?id=${pedido.id}&criado=1`;
            return;
        }

        mostrarMensagem(mensagem,
            await mensagemDeErro(resposta, "Não foi possível criar o pedido."), "erro");
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
    } finally {
        botaoCriar.disabled = false;
    }
});

formPedido.addEventListener("reset", () => {
    mostrarMensagem(mensagem, "");
    setTimeout(prepararFormulario);
});

// -------------------------------------------------------------------
// Início
// -------------------------------------------------------------------

iniciarLayout({ ativo: "pedidos" }).then((usuario) => {
    if (!usuario) return; // sem sessão: já foi redirecionado ao login
    carregarLista(1);
    carregarOpcoes();
});
