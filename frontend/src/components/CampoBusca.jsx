import { useId, useState } from 'react'
import { IconeSetaBaixo } from './Icones.jsx'
import './CampoBusca.css'

// "Lúpulo" e "lupulo" contam como iguais. Normaliza letra por letra para manter as posições
// do texto original (usadas no destaque do trecho encontrado).
const normalizar = (texto) =>
    [...texto].map((c) => c.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()[0] ?? '').join('')

function Destaque({ texto, termo }) {
    const inicio = termo ? normalizar(texto).indexOf(termo) : -1
    if (inicio < 0) return texto
    const fim = inicio + termo.length
    return (
        <>
            {texto.slice(0, inicio)}
            <strong>{texto.slice(inicio, fim)}</strong>
            {texto.slice(fim)}
        </>
    )
}

/**
 * Campo de texto com lista para buscar e escolher (combobox).
 * opcoes: [{ id, nome, detalhe? }]  ·  valor: id escolhido ou null
 * criar (opcional): { rotulo: (texto) => string, aoCriar: (texto) => void } — vira a última opção da lista
 */
export default function CampoBusca({
    id,
    opcoes,
    valor,
    aoEscolher,
    placeholder = 'Digite ou escolha na lista',
    criar,
    invalido,
    descritoPor,
}) {
    const idLista = useId()
    const [texto, setTexto] = useState('')
    const [editando, setEditando] = useState(false)
    const [aberto, setAberto] = useState(false)
    const [ativo, setAtivo] = useState(0)

    const selecionada = opcoes.find((o) => o.id === valor) ?? null
    const termo = editando ? normalizar(texto.trim()) : ''
    const filtradas = opcoes.filter((o) => normalizar(o.nome).includes(termo))
    const podeCriar = criar && texto.trim() && !opcoes.some((o) => normalizar(o.nome) === termo)

    // A opção de criar entra no fim da lista, para funcionar também pelo teclado
    const itens = [...filtradas.map((o) => ({ tipo: 'opcao', opcao: o })), ...(podeCriar ? [{ tipo: 'criar' }] : [])]

    function escolher(item) {
        if (item.tipo === 'criar') {
            criar.aoCriar(texto.trim())
        } else {
            aoEscolher(item.opcao)
        }
        setEditando(false)
        setAberto(false)
    }

    function digitar(e) {
        setTexto(e.target.value)
        setEditando(true)
        setAberto(true)
        setAtivo(0)
        if (valor != null) aoEscolher(null)
    }

    function teclar(e) {
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault()
            if (!aberto) return setAberto(true)
            const passo = e.key === 'ArrowDown' ? 1 : -1
            setAtivo((a) => (itens.length ? (a + passo + itens.length) % itens.length : 0))
        } else if (e.key === 'Enter' && aberto) {
            e.preventDefault()
            if (itens[ativo]) escolher(itens[ativo])
        } else if (e.key === 'Escape' && aberto) {
            e.preventDefault()
            setAberto(false)
        }
    }

    const idItem = (i) => `${idLista}-${i}`

    return (
        <div className="campo-busca">
            <div className="campo-busca__caixa">
                <input
                    id={id}
                    type="text"
                    role="combobox"
                    autoComplete="off"
                    aria-autocomplete="list"
                    aria-expanded={aberto}
                    aria-controls={idLista}
                    aria-activedescendant={aberto && itens[ativo] ? idItem(ativo) : undefined}
                    aria-invalid={invalido || undefined}
                    aria-describedby={descritoPor}
                    placeholder={placeholder}
                    value={editando ? texto : (selecionada?.nome ?? '')}
                    onChange={digitar}
                    onKeyDown={teclar}
                    onFocus={() => setAberto(true)}
                    onClick={() => setAberto(true)}
                    onBlur={() => setAberto(false)}
                />
                <button
                    type="button"
                    tabIndex={-1}
                    className="campo-busca__seta"
                    aria-label="Abrir lista"
                    onMouseDown={(e) => {
                        // mantém o foco no texto
                        e.preventDefault()
                        document.getElementById(id)?.focus()
                        setAberto((a) => !a)
                    }}
                >
                    <IconeSetaBaixo tamanho={18} />
                </button>
            </div>

            {aberto && (
                <ul className="campo-busca__lista" id={idLista} role="listbox">
                    {itens.length === 0 && <li className="campo-busca__nada">Nada encontrado.</li>}
                    {itens.map((item, i) => (
                        <li
                            key={item.tipo === 'criar' ? 'criar' : item.opcao.id}
                            id={idItem(i)}
                            role="option"
                            aria-selected={i === ativo}
                            className={`campo-busca__item${i === ativo ? ' campo-busca__item--ativo' : ''}${
                                item.tipo === 'criar' ? ' campo-busca__item--criar' : ''
                            }`}
                            onMouseDown={(e) => e.preventDefault()}
                            onMouseEnter={() => setAtivo(i)}
                            onClick={() => escolher(item)}
                        >
                            {item.tipo === 'criar' ? (
                                `+ ${criar.rotulo(texto.trim())}`
                            ) : (
                                <>
                                    <span>
                                        <Destaque texto={item.opcao.nome} termo={termo} />
                                    </span>
                                    {item.opcao.detalhe && <small>{item.opcao.detalhe}</small>}
                                </>
                            )}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    )
}
