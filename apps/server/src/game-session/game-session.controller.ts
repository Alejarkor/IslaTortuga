import { Controller, Post, Req, UseGuards } from '@nestjs/common';
import { Request } from 'express';
import { AuthGuard } from '../auth/auth.guard';
import { GameSessionService } from './game-session.service';

type AuthenticatedRequest = Request & {
  user: {
    id: string;
    email: string;
  };
};

@Controller('dev')
export class GameSessionController {
  constructor(private readonly gameSessionService: GameSessionService) {}

  @UseGuards(AuthGuard)
  @Post('start-game')
  startGame(@Req() request: AuthenticatedRequest) {
    return this.gameSessionService.createDevStartGame(request.user.id);
  }
}
