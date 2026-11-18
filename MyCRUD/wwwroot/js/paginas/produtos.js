// Código da página produtos.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

// Estado da tela: página atual da lista e produto em edição (null = cadastrando).
const estado = { pagina: 1, emEdicao: null };

const mensagem = document.querySelector("#mensagem");
const filtro = document.querySelector("#filtro");
const linhas = document.querySelector("#linhas");
const contagem = document.querySelector("#contagem");
const paginador = document.querySelector("#paginador");
const formulario = document.querySelector("#formulario");
const tituloFormulario = document.querySelector("#titulo-formulario");
const salvar = document.querySelector("#salvar");
const cancelar = document.querySelector("#cancelar");

// -------------------------------------------------------------- categorias (selects)

/**
 * Preenche os dois selects de categoria (filtro e formulário).
 * GET /categorias devolve no máximo 100 por página: suficiente para o porte da confeitaria.
 */
async function carregarCategorias() {
    const resposta = await api("/categorias?tamanho=100");

    if (!resposta.ok) {
        throw new Error(await mensagemDeErro(resposta, "Não foi possível carregar as categorias."));
    }

    const { itens } = await resposta.json();

    for (const select of [filtro.elements.categoriaId, formulario.elements.categoriaId]) {
        // Mantém só a primeira opção ("Todas" / "Selecione") e recria as demais.
        select.replaceChildren(select.options[0]);
        for (const categoria of itens) {
            // new Option(texto, valor): o texto entra como texto puro, sem interpretar HTML.
            select.appendChild(new Option(categoria.nome, categoria.id));
        }
    }
}

// -------------------------------------------------------------- lista

async function carregarLista() {
    const query = montarQuery({
        nome: filtro.elements.nome.value.trim(),
        categoriaId: filtro.elements.categoriaId.value,
        situacao: filtro.elements.situacao.value,
        pagina: estado.pagina
    });

    const resposta = await api(`/produtos${query}`);

    if (!resposta.ok) {
        throw new Error(await mensagemDeErro(resposta, "Não foi possível carregar os produtos."));
    }

    const pagina = await resposta.json();

    // A página pedida ficou além do fim (ex.: o último item dela foi desativado
    // com o filtro "Ativos"): volta para a última página que existe.
    if (pagina.itens.length === 0 && pagina.totalPaginas > 0 && estado.pagina > pagina.totalPaginas) {
        estado.pagina = pagina.totalPaginas;
        return carregarLista();
    }

    desenharLinhas(pagina.itens);
    contagem.textContent = pagina.total === 1 ? "1 encontrado" : `${pagina.total} encontrados`;
    montarPaginador(paginador, pagina, (novaPagina) => {
        estado.pagina = novaPagina;
        executar(carregarLista);
    });
}

function desenharLinhas(produtos) {
    linhas.replaceChildren();

    if (produtos.length === 0) {
        const tr = document.createElement("tr");
        const celula = criarCelula("Nenhum produto encontrado com esses filtros.", "lista-vazia");
        celula.colSpan = 6;
        tr.appendChild(celula);
        linhas.appendChild(tr);
        return;
    }

    for (const produto of produtos) {
        const tr = document.createElement("tr");
        if (!produto.ativo) tr.classList.add("inativo");
        if (produto.id === estado.emEdicao?.id) tr.classList.add("em-edicao");

        const situacao = document.createElement("td");
        situacao.appendChild(produto.ativo
            ? criarSelo("Ativo", "ativo")
            : criarSelo("Inativo", "inativo"));

        const acoes = document.createElement("td");
        acoes.className = "acoes";
        acoes.appendChild(criarBotao("Editar", "", () => iniciarEdicao(produto)));

        // .so-admin: some para Operador. Só conforto visual; a API responde 403 de qualquer forma.
        acoes.appendChild(produto.ativo
            ? criarBotao("Desativar", "botao-perigo so-admin", (e) => desativar(produto, e.currentTarget))
            : criarBotao("Reativar", "so-admin", (e) => reativar(produto, e.currentTarget)));

        tr.append(
            criarCelula(produto.id, "id"),
            criarCelula(produto.nome),
            criarCelula(produto.categoriaNome),
            criarCelula(formatarMoeda(produto.preco), "num"),
            situacao,
            acoes);
        linhas.appendChild(tr);
    }
}

// -------------------------------------------------------------- desativar / reativar (Admin)

async function desativar(produto, botao) {
    if (!confirm(`Desativar o produto "${produto.nome}"? Ele deixa de aparecer para novos pedidos.`)) return;
    await mudarSituacao(botao, `/produtos/${produto.id}`, "DELETE", "Produto desativado.");
}

async function reativar(produto, botao) {
    await mudarSituacao(botao, `/produtos/${produto.id}/reativar`, "POST", "Produto reativado.");
}

/** DELETE (desativar) e POST /reativar respondem 204 sem corpo; 403 = sem permissão. */
async function mudarSituacao(botao, url, metodo, textoSucesso) {
    botao.disabled = true;

    try {
        const resposta = await api(url, { method: metodo });

        if (!resposta.ok) {
            throw new Error(resposta.status === 404
                ? "O produto não existe mais."
                : await mensagemDeErro(resposta, "Não foi possível alterar a situação do produto."));
        }

        mostrarMensagem(mensagem, textoSucesso, "sucesso");
        await carregarLista();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    } finally {
        botao.disabled = false;
    }
}

// -------------------------------------------------------------- formulário

function iniciarEdicao(produto) {
    if (salvar.disabled) return; // um envio está em andamento

    estado.emEdicao = produto;
    formulario.elements.nome.value = produto.nome;
    formulario.elements.preco.value = produto.preco;
    formulario.elements.categoriaId.value = String(produto.categoriaId);
    tituloFormulario.textContent = `Editar produto ${produto.id}`;
    salvar.textContent = "Salvar alterações";
    cancelar.hidden = false;
    formulario.classList.add("editando");
    mostrarMensagem(mensagem, "");
    destacarLinhaEmEdicao();
    formulario.elements.nome.focus();
}

function encerrarEdicao() {
    estado.emEdicao = null;
    formulario.reset();
    tituloFormulario.textContent = "Cadastrar produto";
    salvar.textContent = "Cadastrar";
    cancelar.hidden = true;
    formulario.classList.remove("editando");
    destacarLinhaEmEdicao();
}

/** Marca na tabela a linha do produto em edição sem recarregar a lista. */
function destacarLinhaEmEdicao() {
    for (const tr of linhas.rows) {
        const id = Number(tr.cells[0]?.textContent);
        tr.classList.toggle("em-edicao", id === estado.emEdicao?.id);
    }
}

cancelar.addEventListener("click", () => {
    encerrarEdicao();
    mostrarMensagem(mensagem, "Edição cancelada.", "info");
});

formulario.addEventListener("submit", async (evento) => {
    evento.preventDefault();

    const editando = estado.emEdicao !== null;
    salvar.disabled = true;
    cancelar.disabled = true;

    try {
        // PUT não envia "ativo": a situação só muda pelos botões de Admin na lista.
        const resposta = await api(editando ? `/produtos/${estado.emEdicao.id}` : "/produtos", {
            method: editando ? "PUT" : "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                nome: formulario.elements.nome.value,
                preco: Number(formulario.elements.preco.value),
                categoriaId: Number(formulario.elements.categoriaId.value)
            })
        });

        if (!resposta.ok) {
            throw new Error(resposta.status === 404
                ? "O produto não existe mais. Cancele a edição e atualize a lista."
                : await mensagemDeErro(resposta, "Não foi possível salvar. Confira os dados."));
        }

        encerrarEdicao();
        mostrarMensagem(mensagem, editando ? "Produto atualizado." : "Produto cadastrado.", "sucesso");
        await carregarLista();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    } finally {
        salvar.disabled = false;
        cancelar.disabled = false;
    }
});

// -------------------------------------------------------------- filtro

filtro.addEventListener("submit", (evento) => {
    evento.preventDefault();
    estado.pagina = 1;
    executar(carregarLista);
});

// "Limpar" volta os campos ao padrão do HTML (Situação = Ativos).
// O evento reset acontece ANTES de os campos serem limpos, por isso o setTimeout.
filtro.addEventListener("reset", () => {
    setTimeout(() => {
        estado.pagina = 1;
        executar(carregarLista);
    });
});

// -------------------------------------------------------------- início

/** Roda uma ação assíncrona e mostra o erro dela na mensagem da página. */
async function executar(acao) {
    try {
        await acao();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    }
}

iniciarLayout({ ativo: "produtos" }).then(async (usuario) => {
    if (!usuario) return; // sem sessão: já foi redirecionado ao login

    // Categorias e lista são independentes: se uma falhar, a outra ainda aparece.
    await executar(carregarCategorias);
    await executar(carregarLista);
});
