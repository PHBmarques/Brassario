// Guarda o token de acesso EM MEMÓRIA (e não no localStorage), para reduzir o risco de roubo por XSS.
// Ao recarregar a página, a sessão é restaurada pelo refresh token (cookie HttpOnly).

let accessToken = null
let aoExpirar = null

export const getToken = () => accessToken
export const setToken = (token) => { accessToken = token }
export const clearToken = () => { accessToken = null }

// O AuthContext (FE-2) registra aqui o que fazer quando a sessão expira (ex.: ir para /login)
export const setUnauthorizedHandler = (fn) => { aoExpirar = fn }
export const notifyUnauthorized = () => { if (aoExpirar) aoExpirar() }