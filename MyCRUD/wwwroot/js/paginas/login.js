// Código da página login.html. Carregado como arquivo (e não <script> inline) por causa da
// Content-Security-Policy "script-src 'self'" enviada pelo servidor.

const formulario = document.querySelector("#form-login");
const mensagem = document.querySelector("#mensagem");

formulario.addEventListener("submit", async (evento) => {
    evento.preventDefault();

    const botao = formulario.querySelector("button");
    botao.disabled = true;
    mensagem.textContent = "Verificando acesso...";

    try {
        const resposta = await api("/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                email: formulario.elements.email.value,
                senha: formulario.elements.senha.value
            })
        });

        if (resposta.ok) {
            // Pedidos é a tela de uso diário (Admin e Operador começam por ela).
            // O papel vem na resposta ({ ..., papel: "Admin" }), mas cada tela
            // o relê de /login/eu, então não é preciso guardá-lo aqui.
            window.location.href = "/pedidos.html";
            mensagem.textContent = "Login realizado!";
            formulario.elements.senha.value = "";
        } else if (resposta.status === 401) {
            // A API não diz se o erro foi no e-mail ou na senha, de propósito.
            mensagem.textContent = "E-mail ou senha inválidos.";
        } else {
            mensagem.textContent = await mensagemDeErro(
                resposta, "Não foi possível entrar.");
        }
    } catch {
        mensagem.textContent = "Não foi possível conectar ao servidor.";
    } finally {
        botao.disabled = false;
    }
});
