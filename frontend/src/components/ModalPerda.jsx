import { useEffect, useRef, useState } from 'react'
import { registrarPerda } from '../Api/perdas.js'
import { errosPorCampo, mensagemDeErro } from '../Api/erros.js'
import { formatarQuantidade } from '../Utils/armario.js'
import { validarPerda } from '../Utils/perda.js'
import './ModalPerda.css'

// Janela para registrar a perda de um lote. alerta: { loteId, codigoLote, insumoNome, saldo, unidadeMedida }
export default function ModalPerda({ alerta, aoFechar, aoConcluir }) {
    const [form, setForm] = useState({ quantidade: String(alerta.saldo).replace('.', ','), motivo: '' })
    const [erros, setErros] = useState({})
    const [erroGeral, setErroGeral] = useState('')
    const [salvando, setSalvando] = useState(false)
    const primeiroCampo = useRef(null)

    useEffect(() => {
        primeiroCampo.current?.focus()
        const esc = (e) => {
            if (e.key === 'Escape') aoFechar()
        }
        document.addEventListener('keydown', esc)
        return () => document.removeEventListener('keydown', esc)
    }, [aoFechar])

    const alterar = (campo) => (e) => {
        setForm((f) => ({ ...f, [campo]: e.target.value }))
        setErros((atual) => (atual[campo] ? { ...atual, [campo]: undefined } : atual))
    }

    const saldoTexto = formatarQuantidade(alerta.saldo, alerta.unidadeMedida)

    const enviar = async (e) => {
        e.preventDefault()
        const { erros: encontrados, quantidade } = validarPerda(form, alerta.saldo, () => saldoTexto)
        setErros(encontrados)
        setErroGeral('')
        if (Object.keys(encontrados).length > 0) return

        setSalvando(true)
        try {
            await registrarPerda(alerta.loteId, { quantidade, motivo: form.motivo.trim() })
            aoConcluir(alerta)
        } catch (err) {
            setErros(errosPorCampo(err))
            setErroGeral(mensagemDeErro(err))
            setSalvando(false)
        }
    }

    return (
        <div className="perda__fundo" onMouseDown={(e) => e.target === e.currentTarget && !salvando && aoFechar()}>
            <form
                className="perda"
                role="dialog"
                aria-modal="true"
                aria-labelledby="perda-titulo"
                onSubmit={enviar}
                noValidate
            >
                <h2 id="perda-titulo" className="perda__titulo">Registrar perda</h2>
                <p className="perda__texto">
                    {alerta.insumoNome}, lote {alerta.codigoLote}. Saldo do lote: {saldoTexto}.
                </p>

                {erroGeral && (
                    <p className="form-erro" role="alert">
                        {erroGeral}
                    </p>
                )}

                <div className="form-campo">
                    <label htmlFor="perda-quantidade">Quantidade perdida *</label>
                    <input
                        id="perda-quantidade"
                        ref={primeiroCampo}
                        type="text"
                        inputMode="decimal"
                        autoComplete="off"
                        value={form.quantidade}
                        onChange={alterar('quantidade')}
                        disabled={salvando}
                        aria-invalid={Boolean(erros.quantidade)}
                    />
                    {erros.quantidade && <span className="form-campo__erro">{erros.quantidade}</span>}
                </div>

                <div className="form-campo">
                    <label htmlFor="perda-motivo">Motivo *</label>
                    <input
                        id="perda-motivo"
                        type="text"
                        maxLength={255}
                        autoComplete="off"
                        placeholder="Ex.: lote vencido, descartado"
                        value={form.motivo}
                        onChange={alterar('motivo')}
                        disabled={salvando}
                        aria-invalid={Boolean(erros.motivo)}
                    />
                    {erros.motivo && <span className="form-campo__erro">{erros.motivo}</span>}
                </div>

                <div className="form-acoes">
                    <button type="button" className="botao-secundario" onClick={aoFechar} disabled={salvando}>
                        Cancelar
                    </button>
                    <button type="submit" className="botao-primario" disabled={salvando}>
                        {salvando ? 'Registrando…' : 'Registrar perda'}
                    </button>
                </div>
            </form>
        </div>
    )
}
