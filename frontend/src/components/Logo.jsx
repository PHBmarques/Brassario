// Usa o arquivo que já está em public/logo-brassario.png
export default function Logo({ tamanho = 32, alt = '' }) {
    return (
        <img
            src="/logo-brassario.png"
            alt={alt}
            width={tamanho}
            height={tamanho}
            style={{ objectFit: 'contain', display: 'block' }}
        />
    )
}
