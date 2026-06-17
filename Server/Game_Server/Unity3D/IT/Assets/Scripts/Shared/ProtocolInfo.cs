namespace IslaTortuga.Shared
{
    /// <summary>
    /// Punto único para versionar el protocolo de red y los contratos de mensajes
    /// compartidos entre Game Server y cliente. De momento (Fase 0) solo expone la
    /// versión; en fases posteriores aquí vivirán los DTOs de mensajes
    /// ({ type, payload }), ids y enums comunes descritos en el roadmap.
    /// </summary>
    public static class ProtocolInfo
    {
        /// <summary>Versión del protocolo realtime. Se incrementa al romper compatibilidad.</summary>
        public const string Version = "0.0.0-phase0";
    }
}
