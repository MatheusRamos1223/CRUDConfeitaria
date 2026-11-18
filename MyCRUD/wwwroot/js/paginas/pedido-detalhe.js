// Código da página pedido-detalhe.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

const mensagem = document.querySelector("#mensagem");
const acoes = document.querySelector("#acoes");

const parametros = new URLSearchParams(window.location.search);
const idPedido = Number(parametros.get("id"));

/** Papel do usuário logado ("Admin" | "Operador"), preenchido no início. */
let papelUsuario = "Operador";

const CONFIRMAR_CANCELAMENTO = "Cancelar este pedido? Esta ação não pode ser desfeita.";

/**
 * Ações válidas para cada status (tabela 2.6 do contrato, regras R28 e R30).
 * Cancelar um pedido Confirmado é só Admin: o botão tem "so-admin" (some para Operador),
 * mas quem garante a regra é a API, que responde 403.
 */
const TRANSICOES = {
    Aberto: [
        { texto: "Confirmar pedido", rota: "confirmar", classe: "botao-primario", sucesso: "Pedido confirmado." },
        { texto: "Cancelar pedido", rota: "cancelar", classe: "botao-perigo", sucesso: "Pedido cancelado.", pergunta: CONFIRMAR_CANCELAMENTO }
    ],
    Confirmado: [
        { texto: "Marcar como entregue", rota: "entregar", classe: "botao-primario", sucesso: "Pedido marcado como entregue." },
        { texto: "Cancelar pedido", rota: "cancelar", classe: "botao-perigo so-admin", sucesso: "Pedido cancelado.", pergunta: CONFIRMAR_CANCELAMENTO }
    ],
    Entregue: [],
    Cancelado: []
};

// -------------------------------------------------------------------
// Carregar e desenhar
// -------------------------------------------------------------------

/** Busca o pedido. Devolve true se conseguiu desenhar. */
async function carregarPedido() {
    try {
        const resposta = await api(`/pedidos/${idPedido}`);

        if (resposta.status === 404) {
            mostrarMensagem(mensagem, "Pedido não encontrado.", "erro");
            return false;
        }

        if (!resposta.ok) {
            mostrarMensagem(mensagem,
                await mensagemDeErro(resposta, "Não foi possível carregar o pedido."), "erro");
            return false;
        }

        desenharPedido(await resposta.json());
        return true;
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
        return false;
    }
}

/** Desenha a tela inteira a partir de um PedidoDetalheDto. Pode ser chamada de novo após cada ação. */
function desenharPedido(pedido) {
    document.title = `Pedido #${pedido.id} — Confeitaria`;
    document.querySelector("#titulo").textContent = `Pedido #${pedido.id}`;
    document.querySelector("#selo-status").replaceChildren(criarSelo(pedido.status));
    document.querySelector("#criado-em").textContent = formatarDataHora(pedido.criadoEm);

    desenharResumo(pedido);
    desenharAuditoria(pedido);
    desenharLinhaDoTempo(pedido.status);
    desenharItens(pedido);
    desenharAcoes(pedido.status);
}

function desenharResumo(pedido) {
    const quantidadeTotal = pedido.itens.reduce((soma, item) => soma + item.quantidade, 0);
    const campos = [
        ["Cliente", pedido.clienteNome],
        ["Telefone", formatarTelefone(pedido.clienteTelefone)],
        ["Entrega", formatarData(pedido.dataEntrega)],
        ["Itens", String(quantidadeTotal)],
        ["Total", formatarMoeda(pedido.total)]
    ];

    const resumo = document.querySelector("#resumo");
    resumo.replaceChildren();

    for (const [rotulo, valor] of campos) {
        const bloco = document.createElement("div");
        const dt = document.createElement("dt");
        dt.textContent = rotulo;
        const dd = document.createElement("dd");
        dd.textContent = valor;
        bloco.append(dt, dd);
        resumo.appendChild(bloco);
    }
}

/**
 * Quem fez o quê: "Criado por X em …" e "Último status por Y em …".
 * Campos do PedidoDetalheDto: criadoPorNome, criadoEm, statusAlteradoPorNome e
 * statusAlteradoEm (ISO UTC; null enquanto o pedido nunca mudou de status).
 * Pedidos antigos podem não ter autor gravado: aí mostra só a data.
 */
function desenharAuditoria(pedido) {
    const criacao = document.querySelector("#auditoria-criacao");
    const quando = formatarDataHora(pedido.criadoEm);
    criacao.textContent = pedido.criadoPorNome
        ? `Criado por ${pedido.criadoPorNome} em ${quando}`
        : `Criado em ${quando}`;

    const ultimoStatus = document.querySelector("#auditoria-status");
    ultimoStatus.hidden = !pedido.statusAlteradoEm;
    if (!pedido.statusAlteradoEm) {
        ultimoStatus.textContent = "";
        return;
    }

    const quandoStatus = formatarDataHora(pedido.statusAlteradoEm);
    ultimoStatus.textContent = pedido.statusAlteradoPorNome
        ? `Último status por ${pedido.statusAlteradoPorNome} em ${quandoStatus}`
        : `Último status em ${quandoStatus}`;
}

/**
 * Linha do tempo Aberto → Confirmado → Entregue.
 * Cancelado encerra o fluxo: as etapas ficam neutras e uma etapa "Cancelado" aparece como atual.
 */
function desenharLinhaDoTempo(status) {
    const etapas = ["Aberto", "Confirmado", "Entregue"];
    const indiceAtual = etapas.indexOf(status);
    const linhaDoTempo = document.querySelector("#linha-do-tempo");
    linhaDoTempo.replaceChildren();

    etapas.forEach((etapa, i) => {
        const div = document.createElement("div");
        div.className = i < indiceAtual ? "etapa feita" : i === indiceAtual ? "etapa atual" : "etapa";
        div.textContent = etapa;
        linhaDoTempo.appendChild(div);
    });

    if (status === "Cancelado") {
        const cancelado = document.createElement("div");
        cancelado.className = "etapa atual";
        cancelado.textContent = "Cancelado";
        linhaDoTempo.appendChild(cancelado);
    }
}

function desenharItens(pedido) {
    const linhas = document.querySelector("#linhas");
    linhas.replaceChildren();

    for (const item of pedido.itens) {
        const tr = document.createElement("tr");
        tr.append(
            criarCelula(item.produtoNome),
            criarCelula(String(item.quantidade), "num"),
            criarCelula(formatarMoeda(item.precoUnitario), "num"),
            criarCelula(formatarMoeda(item.subtotal), "num"));
        linhas.appendChild(tr);
    }

    document.querySelector("#total").textContent = formatarMoeda(pedido.total);
}

/** Mostra só os botões das transições válidas para o status atual. */
function desenharAcoes(status) {
    acoes.replaceChildren();

    const ajuda = document.createElement("span");
    ajuda.className = "ajuda espaco";
    acoes.appendChild(ajuda);

    const disponiveis = TRANSICOES[status] ?? [];

    if (disponiveis.length === 0) {
        ajuda.textContent = "Pedido finalizado: nenhuma ação disponível.";
        return;
    }

    ajuda.textContent = status === "Confirmado" && papelUsuario !== "Admin"
        ? "Cancelar um pedido confirmado exige Admin."
        : "Entregue e Cancelado são finais.";

    for (const acao of disponiveis) {
        const botao = criarBotao(acao.texto, acao.classe, () => executarAcao(acao));
        botao.classList.remove("botao-pequeno");
        acoes.appendChild(botao);
    }
}

// -------------------------------------------------------------------
// Ações
// -------------------------------------------------------------------

function habilitarAcoes(habilitar) {
    for (const botao of acoes.querySelectorAll("button")) botao.disabled = !habilitar;
}

/** Chama a rota de transição. A resposta 200 traz o pedido atualizado, que é redesenhado. */
async function executarAcao(acao) {
    if (acao.pergunta && !window.confirm(acao.pergunta)) return;

    mostrarMensagem(mensagem, "");
    habilitarAcoes(false);

    try {
        const resposta = await api(`/pedidos/${idPedido}/${acao.rota}`, { method: "POST" });

        if (resposta.ok) {
            desenharPedido(await resposta.json());
            mostrarMensagem(mensagem, acao.sucesso, "sucesso");
            return;
        }

        const erro = await mensagemDeErro(resposta, "Não foi possível alterar o pedido.");

        // 409 = outro usuário mudou o status antes (R29): recarrega para mostrar a situação real.
        if (resposta.status === 409) await carregarPedido();

        mostrarMensagem(mensagem, erro, "erro");
    } catch {
        mostrarMensagem(mensagem, "Não foi possível conectar ao servidor.", "erro");
    } finally {
        habilitarAcoes(true);
    }
}

// -------------------------------------------------------------------
// Início
// -------------------------------------------------------------------

iniciarLayout({ ativo: "pedidos" }).then(async (usuario) => {
    if (!usuario) return; // sem sessão: já foi redirecionado ao login
    papelUsuario = usuario.papel ?? "Operador";

    if (!Number.isInteger(idPedido) || idPedido < 1) {
        mostrarMensagem(mensagem, "Pedido inválido. Volte à lista e abra um pedido.", "erro");
        return;
    }

    const carregou = await carregarPedido();

    // Vindo da tela de pedidos logo após criar (?criado=1).
    if (carregou && parametros.get("criado") === "1") {
        mostrarMensagem(mensagem, `Pedido #${idPedido} criado.`, "sucesso");
    }
});
