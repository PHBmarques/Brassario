import { useTheme } from '../Theme/ThemeContext.jsx'
import { IconeLua, IconeSol } from './Icones.jsx'
import './ThemeToggle.css'

/**
 * Botão de trocar o tema (só o ícone — usado na tela de login).
 * Tema claro  -> mostra a LUA (clicar leva ao escuro)
 * Tema escuro -> mostra o SOL (clicar leva ao claro)
 */
export default function ThemeToggle({ className = '' }) {
    const { tema, alternarTema } = useTheme()
    const claro = tema === 'light'
    const rotulo = claro ? 'Mudar para o tema escuro' : 'Mudar para o tema claro'

    return (
        <button
            type="button"
            className={`theme-toggle ${className}`}
            onClick={alternarTema}
            aria-label={rotulo}
            title={rotulo}
        >
            {claro ? <IconeLua /> : <IconeSol />}
        </button>
    )
}
