import { EMAIL_REGEX, MINIMO_SENHA, gerarSenhaTemporaria } from './senha'

// Regras do formulário "Novo usuário" (a API valida de novo)
export const PERFIS = [
    { valor: 'OPERADOR', dica: 'O operador faz produções e dá entrada no estoque, mas não edita receitas.' },
    { valor: 'ADMIN', dica: 'O administrador pode tudo: cadastros, receitas, ajustes de estoque e usuários.' },
]

export const novoFormulario = () => ({ nome: '', email: '', perfil: 'OPERADOR', senha: gerarSenhaTemporaria() })

export function validarCadastro(f) {
    const erros = {}
    if (!f.nome.trim()) erros.nome = 'Informe o nome.'
    if (!f.email.trim()) erros.email = 'Informe o e-mail.'
    else if (!EMAIL_REGEX.test(f.email.trim())) erros.email = 'Digite um e-mail válido.'
    if (!f.senha) erros.senha = 'Informe a senha temporária.'
    else if (f.senha.length < MINIMO_SENHA) erros.senha = `Mínimo de ${MINIMO_SENHA} caracteres.`
    return erros
}
