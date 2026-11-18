// Código da página categorias.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

// Regra de segurança da página: todo texto vindo da API entra por textContent
// (criarCelula, criarBotao, mostrarMensagem), nunca por innerHTML.

const mensagem = document.querySelector("#mensagem");
const formFiltro = document.querySelector("#form-filtro");
const filtroNome = document.querySelector("#filtro-nome");
const linhas = document.querySelector("#linhas");
const contagem = document.querySelector("#contagem");
const paginador = document.querySelector("#paginador");
const formulario = document.querySelector("#form-categoria");
const tituloFormulario = document.querySelector("#titulo-formulario");
const campoNome = document.querySelector("#nome");
const salvar = document.querySelector("#salvar");
const cancelar = document.querySelector("#cancelar");

/** Estado da tela: filtro aplicado, página atual e categoria em edição. */
const estado = { nome: "", pagina: 1, idEmEdicao: null, ehAdmin: false };

// Nome que a migração dá às categorias criadas para produtos antigos (R11).
const NOME_AUTOMATICO = /^Categoria -?\d+$/;

// -------------------------------------------------------------------
// Listagem
// -------------------------------------------------------------------

async function carregarLista(pagina = estado.pagina) {
    estado.pagina = pagina;

    let resposta;
    try {
        resposta = await api("/categorias" + montarQuery({ nome: estado.nome, pagina }));
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
        return;
    }

    if (!resposta.ok) {
        mostrarMensagem(mensagem,
            await mensagemDeErro(resposta, "Não foi possível carregar as categorias."), "erro");
        return;
    }

    const resultado = await resposta.json();

    // Página além do fim (ex.: excluiu o último item da última página): volta uma.
    if (resultado.itens.length === 0 && resultado.totalPaginas > 0 && pagina > resultado.totalPaginas) {
        return carregarLista(resultado.totalPaginas);
    }

    desenharLinhas(resultado.itens);
    contagem.textContent = resultado.total === 1 ? "1 encontrada" : `${resultado.total} encontradas`;
    montarPaginador(paginador, resultado, (novaPagina) => carregarLista(novaPagina));
    avisarNomesAutomaticos(resultado.itens);
}

function desenharLinhas(categorias) {
    linhas.replaceChildren();

    if (categorias.length === 0) {
        const tr = document.createElement("tr");
        const celula = criarCelula("Nenhuma categoria encontrada.", "lista-vazia");
        celula.colSpan = 4;
        tr.appendChild(celula);
        linhas.appendChild(tr);
        return;
    }

    for (const categoria of categorias) {
        const tr = document.createElement("tr");
        if (categoria.id === estado.idEmEdicao) tr.classList.add("em-edicao");

        const acoes = document.createElement("td");
        acoes.className = "acoes";
        acoes.appendChild(criarBotao("Editar", "so-admin", () => iniciarEdicao(categoria)));

        // Excluir só faz sentido sem produtos; mesmo assim a API confere e devolve 409 (R9).
        const excluir = criarBotao("Excluir", "botao-perigo so-admin", () => excluirCategoria(categoria, excluir));
        if (categoria.quantidadeProdutos > 0) {
            excluir.disabled = true;
            excluir.title = "Possui produtos — mova-os para outra categoria antes de excluir";
        }
        acoes.appendChild(excluir);

        tr.append(
            criarCelula(categoria.id, "id"),
            criarCelula(categoria.nome),
            criarCelula(categoria.quantidadeProdutos, "num"),
            acoes);
        linhas.appendChild(tr);
    }
}

/** Lembra o Admin de renomear as categorias "Categoria N" criadas pela migração. */
function avisarNomesAutomaticos(categorias) {
    if (!estado.ehAdmin || mensagem.textContent) return;

    if (categorias.some((c) => NOME_AUTOMATICO.test(c.nome))) {
        mostrarMensagem(mensagem,
            "Há categorias criadas automaticamente pela migração (\"Categoria N\") — renomeie-as.", "info");
    }
}

formFiltro.addEventListener("submit", (evento) => {
    evento.preventDefault();
    estado.nome = filtroNome.value.trim();
    carregarLista(1);
});

// "reset" dispara ANTES de o navegador limpar os campos; por isso usamos o estado,
// e não o valor do input.
formFiltro.addEventListener("reset", () => {
    estado.nome = "";
    carregarLista(1);
});

// -------------------------------------------------------------------
// Formulário (cadastrar / editar) — somente Admin
// -------------------------------------------------------------------

function iniciarEdicao(categoria) {
    estado.idEmEdicao = categoria.id;
    campoNome.value = categoria.nome;
    tituloFormulario.textContent = `Editar categoria ${categoria.id}`;
    salvar.textContent = "Salvar alterações";
    cancelar.hidden = false;
    formulario.classList.add("editando");
    marcarLinhaEmEdicao();
    mostrarMensagem(mensagem, "");
    campoNome.focus();
}

function encerrarEdicao() {
    estado.idEmEdicao = null;
    formulario.reset();
    tituloFormulario.textContent = "Nova categoria";
    salvar.textContent = "Cadastrar";
    cancelar.hidden = true;
    formulario.classList.remove("editando");
    marcarLinhaEmEdicao();
}

/** Destaca a linha em edição sem recarregar a lista. */
function marcarLinhaEmEdicao() {
    for (const tr of linhas.rows) {
        const id = Number(tr.cells[0]?.textContent);
        tr.classList.toggle("em-edicao", estado.idEmEdicao !== null && id === estado.idEmEdicao);
    }
}

cancelar.addEventListener("click", () => {
    encerrarEdicao();
    mostrarMensagem(mensagem, "");
});

formulario.addEventListener("submit", async (evento) => {
    evento.preventDefault();

    const nome = campoNome.value.trim();
    if (!nome) {
        mostrarMensagem(mensagem, "Informe o nome.", "erro");
        campoNome.focus();
        return;
    }

    const editando = estado.idEmEdicao !== null;
    const url = editando ? `/categorias/${estado.idEmEdicao}` : "/categorias";

    salvar.disabled = true;
    try {
        const resposta = await api(url, {
            method: editando ? "PUT" : "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ nome })
        });

        if (!resposta.ok) {
            // 404 na edição: outra pessoa excluiu a categoria enquanto o formulário estava aberto.
            if (resposta.status === 404) encerrarEdicao();
            mostrarMensagem(mensagem,
                await mensagemDeErro(resposta, "Não foi possível salvar a categoria."), "erro");
            if (resposta.status === 404) await carregarLista();
            return;
        }

        const categoria = await resposta.json();
        encerrarEdicao();
        mostrarMensagem(mensagem,
            editando ? `Categoria "${categoria.nome}" atualizada.` : `Categoria "${categoria.nome}" cadastrada.`,
            "sucesso");
        await carregarLista();
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
    } finally {
        salvar.disabled = false;
    }
});

// -------------------------------------------------------------------
// Exclusão — somente Admin
// -------------------------------------------------------------------

async function excluirCategoria(categoria, botao) {
    if (!confirm(`Excluir a categoria "${categoria.nome}"?`)) return;

    botao.disabled = true;
    try {
        const resposta = await api(`/categorias/${categoria.id}`, { method: "DELETE" });

        if (!resposta.ok) {
            // 409: ganhou produtos desde a última listagem (R9). 404: já foi excluída.
            mostrarMensagem(mensagem,
                await mensagemDeErro(resposta, "Não foi possível excluir a categoria."), "erro");
            await carregarLista();
            return;
        }

        if (estado.idEmEdicao === categoria.id) encerrarEdicao();
        mostrarMensagem(mensagem, `Categoria "${categoria.nome}" excluída.`, "sucesso");
        await carregarLista();
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
        botao.disabled = false;
    }
}

// -------------------------------------------------------------------
// Início
// -------------------------------------------------------------------

iniciarLayout({ ativo: "categorias" }).then((usuario) => {
    if (!usuario) return; // sem sessão: já foi redirecionado ao login

    // Esconder o formulário é só conforto visual (CSS .so-admin);
    // quem garante a regra é a API, que devolve 403 ao Operador (R2).
    estado.ehAdmin = usuario.papel === "Admin";
    document.querySelector("#aviso-permissao").hidden = estado.ehAdmin;

    carregarLista(1);
});
