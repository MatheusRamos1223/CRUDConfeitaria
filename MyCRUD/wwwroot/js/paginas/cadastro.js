// Código da página cadastro.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

const formulario = document.querySelector("#form-cadastro");
const mensagem = document.querySelector("#mensagem");

formulario.addEventListener("submit", async (evento) => {
    evento.preventDefault();

    const botao = formulario.querySelector("button");
    botao.disabled = true;
    mensagem.textContent = "Cadastrando...";

    try {
        const resposta = await api("/login/cadastro", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                nome: formulario.elements.nome.value,
                email: formulario.elements.email.value,
                senha: formulario.elements.senha.value
            })
        });

        if (resposta.ok) {
            formulario.reset();
            mensagem.textContent =
                "Usuário criado como Operador! Agora você pode fazer login.";
        } else {
            // 400 (validação), 409 (e-mail repetido) e 429 (limite) vêm com a mensagem da API.
            mensagem.textContent = await mensagemDeErro(
                resposta, "Não foi possível cadastrar.");
        }
    } catch {
        mensagem.textContent = "Não foi possível conectar ao servidor.";
    } finally {
        botao.disabled = false;
    }
});
