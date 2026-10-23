import './Etiqueta.css'

// Pílula colorida de status. tipo: 'ok' | 'atencao' | 'critica'
export default function Etiqueta({ tipo = 'ok', children }) {
    return <span className={`etiqueta etiqueta--${tipo}`}>{children}</span>
}
