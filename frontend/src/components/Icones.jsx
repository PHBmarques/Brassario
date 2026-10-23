// Ícones de linha (SVG inline, sem biblioteca). Herdam a cor do texto (currentColor).
function Base({ tamanho = 22, children, ...resto }) {
    return (
        <svg
            viewBox="0 0 24 24"
            width={tamanho}
            height={tamanho}
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
            focusable="false"
            {...resto}
        >
            {children}
        </svg>
    )
}

export const IconeInicio = (p) => (
    <Base {...p}>
        <path d="M3 11l9-8 9 8" />
        <path d="M5 10v10h5v-6h4v6h5V10" />
    </Base>
)

export const IconeArmario = (p) => (
    <Base {...p}>
        <rect x="3" y="4" width="18" height="4" rx="1" />
        <path d="M5 8v11a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8" />
        <path d="M10 12h4" />
    </Base>
)

export const IconeEntrada = (p) => (
    <Base {...p}>
        <path d="M12 4v11" />
        <path d="M7 10l5 5 5-5" />
        <path d="M4 20h16" />
    </Base>
)

export const IconeReceitas = (p) => (
    <Base {...p}>
        <path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3V4z" />
        <path d="M5 17a3 3 0 0 1 3-3h11" />
    </Base>
)

export const IconeProduzir = (p) => (
    <Base {...p}>
        <path d="M9 3h6" />
        <path d="M10 3v6L4.5 19a1.5 1.5 0 0 0 1.3 2.2h12.4a1.5 1.5 0 0 0 1.3-2.2L14 9V3" />
        <path d="M7.5 15h9" />
    </Base>
)

export const IconeProducoes = (p) => (
    <Base {...p}>
        <path d="M9 6h11M9 12h11M9 18h11" />
        <path d="M4 6h.01M4 12h.01M4 18h.01" />
    </Base>
)

export const IconeAlertas = (p) => (
    <Base {...p}>
        <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
        <path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" />
    </Base>
)

export const IconeUsuarios = (p) => (
    <Base {...p}>
        <circle cx="9" cy="8" r="3.5" />
        <path d="M2.5 20a6.5 6.5 0 0 1 13 0" />
        <path d="M16 4.5a3.5 3.5 0 0 1 0 7" />
        <path d="M18 14.5a6.5 6.5 0 0 1 3.5 5.5" />
    </Base>
)

export const IconeLua = (p) => (
    <Base {...p}>
        <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
    </Base>
)

export const IconeSol = (p) => (
    <Base {...p}>
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </Base>
)

export const IconeRecolher = (p) => (
    <Base {...p}>
        <path d="M11 17l-5-5 5-5" />
        <path d="M18 17l-5-5 5-5" />
    </Base>
)

export const IconeExpandir = (p) => (
    <Base {...p}>
        <path d="M13 17l5-5-5-5" />
        <path d="M6 17l5-5-5-5" />
    </Base>
)

export const IconeSetaCima = (p) => (
    <Base {...p}>
        <path d="M6 15l6-6 6 6" />
    </Base>
)

export const IconeSetaBaixo = (p) => (
    <Base {...p}>
        <path d="M6 9l6 6 6-6" />
    </Base>
)
