// Código da página clientes.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

// Estado da tela: o que está sendo filtrado, a página atual e o cliente em edição.
const estado = { busca: "", pagina: 1, idEmEdicao: null };

const mensagem = document.querySelector("#mensagem");
const filtro = document.querySelector("#filtro");
const linhas = document.querySelector("#linhas");
const listaVazia = document.querySelector("#lista-vazia");
const contagem = document.querySelector("#contagem");
const paginador = document.querySelector("#paginador");
const formulario = document.querySelector("#formulario");
const tituloFormulario = document.querySelector("#titulo-formulario");
const salvar = document.querySelector("#salvar");
const cancelar = document.querySelector("#cancelar");
const campoTelefone = formulario.elements.telefone;

// -------------------------------------------------------------- lista

/**
 * Busca a página atual na API e redesenha a tabela.
 * Se a página ficou vazia (ex.: excluiu o último cliente da última página),
 * volta uma página para o usuário não cair numa lista em branco.
 */
async function carregarLista() {
    const query = montarQuery({ busca: estado.busca, pagina: estado.pagina });
    const resposta = await api(`/clientes${query}`);

    if (!resposta.ok) {
        throw new Error(await mensagemDeErro(resposta, "Não foi possível carregar os clientes."));
    }

    const pagina = await resposta.json();

    if (pagina.itens.length === 0 && pagina.pagina > 1) {
        estado.pagina = Math.max(1, pagina.totalPaginas);
        return carregarLista();
    }

    desenharLinhas(pagina.itens);
    contagem.textContent = pagina.total === 1 ? "1 encontrado" : `${pagina.total} encontrados`;
    montarPaginador(paginador, pagina, (novaPagina) => {
        estado.pagina = novaPagina;
        atualizarLista();
    });
}

function desenharLinhas(clientes) {
    linhas.replaceChildren();
    listaVazia.hidden = clientes.length > 0;

    // Texto vindo da API entra só por textContent (criarCelula/criarBotao):
    // um nome como "<script>" aparece escrito, e não é executado.
    for (const cliente of clientes) {
        const linha = document.createElement("tr");
        if (cliente.id === estado.idEmEdicao) linha.classList.add("em-edicao");

        const acoes = document.createElement("td");
        acoes.className = "acoes";
        acoes.append(
            criarBotao("Editar", "", () => iniciarEdicao(cliente)),
            // so-admin: some para Operador. É só conforto visual; a API devolve 403.
            criarBotao("Excluir", "botao-perigo so-admin", (evento) => excluir(cliente, evento.currentTarget)));

        linha.append(
            criarCelula(cliente.id, "id"),
            criarCelula(cliente.nome),
            criarCelula(formatarTelefone(cliente.telefone)),
            criarCelula(cliente.email ?? "—"),
            acoes);
        linhas.appendChild(linha);
    }
}

/** Carrega a lista mostrando o erro na mensagem, em vez de deixar a promessa rejeitada. */
async function atualizarLista() {
    try {
        await carregarLista();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    }
}

filtro.addEventListener("submit", (evento) => {
    evento.preventDefault();
    estado.busca = filtro.elements.busca.value.trim();
    estado.pagina = 1; // novo filtro sempre começa da primeira página
    mostrarMensagem(mensagem, "");
    atualizarLista();
});

// "reset" limpa o campo sozinho; aqui só voltamos o estado e recarregamos.
filtro.addEventListener("reset", () => {
    estado.busca = "";
    estado.pagina = 1;
    mostrarMensagem(mensagem, "");
    atualizarLista();
});

// -------------------------------------------------------------- formulário

function iniciarEdicao(cliente) {
    if (salvar.disabled) return; // um envio está em andamento

    estado.idEmEdicao = cliente.id;
    formulario.elements.nome.value = cliente.nome;
    campoTelefone.value = formatarTelefone(cliente.telefone);
    formulario.elements.email.value = cliente.email ?? "";

    tituloFormulario.textContent = `Editar cliente ${cliente.id}`;
    salvar.textContent = "Salvar alterações";
    cancelar.hidden = false;
    formulario.classList.add("editando");
    marcarLinhaEmEdicao();
    mostrarMensagem(mensagem, "");
    formulario.elements.nome.focus();
}

function encerrarEdicao() {
    estado.idEmEdicao = null;
    formulario.reset();
    tituloFormulario.textContent = "Novo cliente";
    salvar.textContent = "Cadastrar";
    cancelar.hidden = true;
    formulario.classList.remove("editando");
    marcarLinhaEmEdicao();
}

/** Destaca na tabela a linha do cliente em edição (a 1ª célula é o ID). */
function marcarLinhaEmEdicao() {
    for (const linha of linhas.rows) {
        const id = Number(linha.cells[0].textContent);
        linha.classList.toggle("em-edicao", id === estado.idEmEdicao);
    }
}

cancelar.addEventListener("click", () => {
    encerrarEdicao();
    mostrarMensagem(mensagem, "Edição cancelada.", "info");
});

// Ao sair do campo, mostra o telefone já formatado: confirma para o usuário
// que os dígitos foram entendidos. Valor inválido fica como está; a API explica o erro.
campoTelefone.addEventListener("blur", () => {
    const digitos = campoTelefone.value.replace(/\D/g, "");
    if (digitos.length === 10 || digitos.length === 11) {
        campoTelefone.value = formatarTelefone(digitos);
    }
});

formulario.addEventListener("submit", async (evento) => {
    evento.preventDefault();

    const editando = estado.idEmEdicao !== null;
    const email = formulario.elements.email.value.trim();

    salvar.disabled = true;
    cancelar.disabled = true;

    try {
        const resposta = await api(editando ? `/clientes/${estado.idEmEdicao}` : "/clientes", {
            method: editando ? "PUT" : "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                nome: formulario.elements.nome.value,
                // Vai com a máscara mesmo: a API remove o que não for dígito.
                telefone: campoTelefone.value,
                // Contrato: e-mail vazio vai como null (texto vazio a API recusa).
                email: email === "" ? null : email
            })
        });

        if (!resposta.ok) {
            throw new Error(resposta.status === 404
                ? "Este cliente não existe mais. Cancele a edição e atualize a lista."
                : await mensagemDeErro(resposta, "Não foi possível salvar. Confira os dados."));
        }

        encerrarEdicao();
        mostrarMensagem(mensagem, editando ? "Cliente atualizado." : "Cliente cadastrado.", "sucesso");
        await carregarLista();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    } finally {
        salvar.disabled = false;
        cancelar.disabled = false;
    }
});

// -------------------------------------------------------------- exclusão (Admin)

async function excluir(cliente, botao) {
    if (!confirm(`Excluir o cliente "${cliente.nome}"?`)) return;

    botao.disabled = true;

    try {
        const resposta = await api(`/clientes/${cliente.id}`, { method: "DELETE" });

        // 404: alguém já excluiu; para quem está na tela o resultado é o mesmo.
        if (!resposta.ok && resposta.status !== 404) {
            // 409 traz o title "Cliente possui pedidos."; 403, a falta de permissão.
            throw new Error(await mensagemDeErro(resposta, "Não foi possível excluir o cliente."));
        }

        if (estado.idEmEdicao === cliente.id) encerrarEdicao();
        mostrarMensagem(mensagem, "Cliente excluído.", "sucesso");
        await carregarLista();
    } catch (erro) {
        mostrarMensagem(mensagem, erro.message, "erro");
    } finally {
        botao.disabled = false;
    }
}

// -------------------------------------------------------------- início

// Sem sessão, iniciarLayout já redirecionou para o login e devolve null.
iniciarLayout({ ativo: "clientes" }).then((usuario) => {
    if (!usuario) return;
    atualizarLista();
});
