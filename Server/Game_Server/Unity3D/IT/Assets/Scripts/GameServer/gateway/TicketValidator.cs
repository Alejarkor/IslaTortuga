using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>Ticket validado: a qué partida y jugador pertenece.</summary>
    public sealed class ValidatedTicket
    {
        public string TicketId { get; }
        public string MatchId { get; }
        public string PlayerId { get; }

        public ValidatedTicket(string ticketId, string matchId, string playerId)
        {
            TicketId = ticketId;
            MatchId = matchId;
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Valida y consume un ticket de unión. Es una interfaz para poder inyectar un
    /// doble en los tests sin un backend real.
    /// </summary>
    public interface ITicketValidator
    {
        Task<ValidatedTicket> ValidateAndConsumeAsync(string ticketId);
    }

    /// <summary>
    /// Implementación real: consume el ticket llamando al GameApi
    /// (POST /internal/tickets/consume). El consumo es atómico en el backend (un
    /// segundo intento con el mismo ticket devuelve null), lo que evita la reutilización.
    /// Devuelve null si el ticket es inválido, caducado o ya consumido.
    /// </summary>
    public sealed class HttpTicketValidator : ITicketValidator
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        private readonly string _gameApiUrl;
        private readonly IServerLogger _logger;

        public HttpTicketValidator(string gameApiUrl, IServerLogger logger)
        {
            _gameApiUrl = (gameApiUrl ?? "http://localhost:3001").TrimEnd('/');
            _logger = logger;
        }

        public async Task<ValidatedTicket> ValidateAndConsumeAsync(string ticketId)
        {
            if (string.IsNullOrWhiteSpace(ticketId))
            {
                return null;
            }

            try
            {
                var body = "{\"ticketId\":" + Json.Str(ticketId) + "}";
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await Http.PostAsync(_gameApiUrl + "/internal/tickets/consume", content)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return null; // 404 = inválido/ya consumido
                }

                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!(JsonReader.Parse(text) is Dictionary<string, object> obj))
                {
                    return null;
                }
                if (!(obj.TryGetValue("ticket", out var t) && t is IDictionary<string, object> ticket))
                {
                    return null;
                }

                var matchId = JsonReader.GetString((IDictionary<string, object>)ticket, "matchId");
                var playerId = JsonReader.GetString((IDictionary<string, object>)ticket, "playerId");
                if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(playerId))
                {
                    return null;
                }
                return new ValidatedTicket(ticketId, matchId, playerId);
            }
            catch (Exception ex)
            {
                _logger?.Error("Error validando ticket contra el GameApi.", ex);
                return null;
            }
        }
    }
}
