// Funções compartilhadas pelas páginas para conversar com a API.
//
// A API devolve erros no formato Problem Details (RFC 9457):
//   { "title": "Dados inválidos.", "status": 400,
//     "errors": { "Nome": ["Informe o nome."] } }
// Estas funções transformam esse JSON em uma frase para mostrar ao usuário.

/** Leva o usuário para a tela de login. */
function irParaLogin() {
    window.location.href = "/login.html";
}

/**
 * Confere se ainda existe sessão ativa (GET /login/eu).
 * Sem sessão, redireciona para o login e devolve null.
 * Com sessão, devolve { id, nome, email, papel } (papel: "Admin" ou "Operador").
 */
async function exigirLogin() {
    const resposta = await fetch("/login/eu");

    if (resposta.status === 401) {
        irParaLogin();
        return null;
    }

    return resposta.ok ? await resposta.json() : null;
}

/**
 * Monta a mensagem de erro de uma resposta HTTP que falhou.
 * - 401: sessão expirou → redireciona ao login.
 * - 429: limite de tentativas atingido.
 * - 403: usuário sem o papel exigido (ex.: Operador em rota de Admin).
 * - 400 com "errors": junta as mensagens de validação de cada campo.
 * - Outros: usa o "title" do Problem Details ou a mensagem padrão.
 */
async function mensagemDeErro(resposta, mensagemPadrao) {
    if (resposta.status === 401) {
        irParaLogin();
        return "Sessão expirada. Faça login novamente.";
    }

    if (resposta.status === 429) {
        return "Muitas tentativas. Aguarde um minuto e tente de novo.";
    }

    // A API recusou por falta de permissão (papel). Esconder botões na tela é só
    // conforto visual: quem decide é a API, e aqui só traduzimos a recusa.
    if (resposta.status === 403) {
        return "Você não tem permissão para esta ação.";
    }

    try {
        const problema = await resposta.json();

        if (problema.errors) {
            return Object.values(problema.errors).flat().join(" ");
        }

        if (problema.title) {
            return problema.title;
        }
    } catch {
        // Corpo vazio ou não-JSON: cai na mensagem padrão.
    }

    return mensagemPadrao;
}
