// Regras de senha do lado da tela (as mesmas da API: Security/PasswordPolicy.cs)
export const MINIMO_SENHA = 8

export const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Valida { novaSenha, confirmarSenha } e devolve os erros por campo
export function validarNovaSenha({ novaSenha, confirmarSenha }) {
    const erros = {}
    if (!novaSenha) erros.novaSenha = 'Informe a nova senha.'
    else if (novaSenha.length < MINIMO_SENHA) erros.novaSenha = `A senha precisa ter no mínimo ${MINIMO_SENHA} caracteres.`

    if (!confirmarSenha) erros.confirmarSenha = 'Repita a nova senha.'
    else if (novaSenha !== confirmarSenha) erros.confirmarSenha = 'As senhas não são iguais.'
    return erros
}

// Senha temporária fácil de ditar (sem 0/O, 1/l/I). Ex.: Kx7p-92Qa
const CARACTERES = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'

export function gerarSenhaTemporaria() {
    const bytes = new Uint32Array(8)
    crypto.getRandomValues(bytes)
    const letras = Array.from(bytes, (n) => CARACTERES[n % CARACTERES.length]).join('')
    return `${letras.slice(0, 4)}-${letras.slice(4)}`
}
