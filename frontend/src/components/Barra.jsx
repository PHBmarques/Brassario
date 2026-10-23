import './Barra.css'

// Barra de progresso. status: 'ok' | 'atencao' | 'critica' | 'vazio'
export default function Barra({ nivel, status = 'ok', rotulo }) {
    return (
        <div
            className="barra"
            role="progressbar"
            aria-label={rotulo}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={nivel}
        >
            <div className={`barra__preenchimento barra__preenchimento--${status}`} style={{ width: `${nivel}%` }} />
        </div>
    )
}
