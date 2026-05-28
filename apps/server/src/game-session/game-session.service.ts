import { Injectable } from '@nestjs/common';
import { readFileSync } from 'node:fs';
import { createHmac, randomUUID } from 'node:crypto';
import { PrismaService } from '../prisma/prisma.service';
import { resolveContentIndexPath } from './content-paths';

const DEFAULT_PLAYER_VISUAL_ID = 'player.default';

type ContentPackIndex = {
  defaultContentPackId: string;
  packs: Array<{
    contentPackId: string;
    version: string;
    sceneId: string;
    manifestUrl: string;
  }>;
};

@Injectable()
export class GameSessionService {
  constructor(private readonly prisma: PrismaService) {}

  async createDevStartGame(userId: string) {
    const index = this.loadContentIndex();
    const defaultPack = index.packs.find(
      (pack) => pack.contentPackId === index.defaultContentPackId,
    );

    if (!defaultPack) {
      throw new Error('No existe content pack por defecto para start-game.');
    }

    const user = await this.prisma.user.findUnique({
      where: { id: userId },
      include: {
        profile: true,
      },
    });

    if (!user) {
      throw new Error('No existe el usuario solicitado para start-game.');
    }

    const playerVisualId = user.profile?.playerVisualId ?? DEFAULT_PLAYER_VISUAL_ID;
    const gameTicket = this.signGameTicket({
      ticketId: randomUUID().replace(/-/g, ''),
      userId: user.id,
      displayName: user.profile?.nickname ?? user.email,
      visualId: playerVisualId,
      purpose: 'join',
      previousSessionId: null,
      expiresAt: Date.now() + 30_000,
    });

    return {
      roomId: 'dev-room-001',
      gameUrl: '/game',
      gameTicket,
      contentPackId: defaultPack.contentPackId,
      contentVersion: defaultPack.version,
      sceneId: defaultPack.sceneId,
      manifestUrl: defaultPack.manifestUrl,
      webSocketUrl: '/ws/game',
      localPlayerAppearance: {
        visualId: playerVisualId,
      },
    };
  }

  private loadContentIndex(): ContentPackIndex {
    const indexPath = resolveContentIndexPath();
    const fileContents = readFileSync(indexPath, 'utf8');
    return JSON.parse(fileContents) as ContentPackIndex;
  }

  private signGameTicket(payload: {
    ticketId: string;
    userId: string;
    displayName: string;
    visualId: string;
    purpose: 'join' | 'reconnect';
    previousSessionId: string | null;
    expiresAt: number;
  }) {
    const serializedPayload = Buffer.from(JSON.stringify(payload)).toString('base64url');
    const signature = createHmac('sha256', this.getTicketSecret())
      .update(serializedPayload)
      .digest('base64url');

    return `${serializedPayload}.${signature}`;
  }

  private getTicketSecret() {
    return process.env.GAME_TICKET_SECRET ?? 'dev_game_ticket_secret_change_me';
  }
}
