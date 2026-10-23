import { useState } from 'react'
import { IconeOlho, IconeOlhoFechado } from './Icones.jsx'
import './CampoSenha.css'

// Campo de senha com o "olhinho" para mostrar/ocultar o que foi digitado.
// Todas as props vão para o <input> (id, name, value, onChange, autoComplete, aria-*...).
export default function CampoSenha({ disabled, ...props }) {
    const [visivel, setVisivel] = useState(false)
    const rotulo = visivel ? 'Ocultar senha' : 'Mostrar senha'

    return (
        <div className="campo-senha">
            <input {...props} type={visivel ? 'text' : 'password'} disabled={disabled} />
            <button
                type="button"
                className="campo-senha__olho"
                onClick={() => setVisivel((v) => !v)}
                aria-label={rotulo}
                aria-pressed={visivel}
                aria-controls={props.id}
                title={rotulo}
                disabled={disabled}
            >
                {visivel ? <IconeOlhoFechado tamanho={20} /> : <IconeOlho tamanho={20} />}
            </button>
        </div>
    )
}
