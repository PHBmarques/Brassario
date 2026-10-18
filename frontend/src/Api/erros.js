// Converte o erro do Axios em uma mensagem amigável.
// A API devolve: { timestamp, status, codigo, mensagem, detalhes: [{ campo, erro }] }
export function mensagemDeErro(error) {
    if (!error.response) {
        return 'Não foi possível conectar ao servidor. Verifique sua conexão e tente de novo.'
    }

    const { status, data } = error.response

    if (status === 401) return data?.mensagem ?? 'Sessão expirada ou credenciais inválidas.'
    if (status === 403) return 'Você não tem permissão para fazer isso.'
    if (status === 404) return data?.mensagem ?? 'Item não encontrado.'
    if (status >= 500) return 'Ocorreu um erro no servidor. Tente novamente em instantes.'

    // 400, 409, 422: mensagem de validação ou de regra de negócio
    return data?.mensagem ?? 'Não foi possível concluir a operação.'
}

// Erros de validação por campo, para mostrar junto de cada input: { email: 'E-mail inválido' }
export function errosPorCampo(error) {
    const detalhes = error.response?.data?.detalhes
    if (!Array.isArray(detalhes)) return {}
    return Object.fromEntries(detalhes.filter((d) => d.campo).map((d) => [d.campo, d.erro]))
}