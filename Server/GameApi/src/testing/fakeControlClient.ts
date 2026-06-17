import { genId } from "../ids";
import { MatchConfig } from "../rooms/types";
import {
  CapacityInfo,
  CreatedMatch,
  GameServerControlClient,
  NoCapacityError
} from "../gameserver/controlClient";

/**
 * Doble del Game Server para tests. Simula capacidad finita y guarda las partidas
 * "creadas" para poder hacer aserciones sobre ellas.
 */
export class FakeControlClient implements GameServerControlClient {
  readonly created: Array<{ matchId: string; config: MatchConfig }> = [];
  readonly stopped: string[] = [];

  constructor(private maxMatches = 10) {}

  async getCapacity(): Promise<CapacityInfo> {
    const active = this.created.length - this.stopped.length;
    return {
      canAcceptMatch: active < this.maxMatches,
      availableSlots: Math.max(0, this.maxMatches - active),
      maxMatches: this.maxMatches,
      activeMatches: active
    };
  }

  async createMatch(config: MatchConfig): Promise<CreatedMatch> {
    const active = this.created.length - this.stopped.length;
    if (active >= this.maxMatches) {
      throw new NoCapacityError();
    }
    const matchId = genId("match");
    this.created.push({ matchId, config });
    return { matchId, gatewayHost: "localhost", gatewayPort: 9090 };
  }

  async stopMatch(matchId: string): Promise<void> {
    this.stopped.push(matchId);
  }
}
