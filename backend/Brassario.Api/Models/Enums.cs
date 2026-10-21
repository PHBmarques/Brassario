namespace Brassario.Api.Models
{
        public enum PerfilUsuario { ADMIN, OPERADOR }

        public enum CategoriaInsumo { MALTE, LUPULO, LEVEDURA, ADJUNTO, OUTRO }

        public enum UnidadeMedida { KG, G, L, ML, UN }

        public enum StatusBatelada { EM_ANDAMENTO, FINALIZADA, CANCELADA }

        public enum TipoMovimentacao { ENTRADA, SAIDA_PRODUCAO, AJUSTE, PERDA, ESTORNO }

        public enum TipoTokenUsuario
        {
            CONFIRMAR_EMAIL,
            REDEFINIR_SENHA
        }
}
