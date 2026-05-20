import { Module } from '@nestjs/common';
import { AuthModule } from '../auth/auth.module';
import { GameSessionController } from './game-session.controller';
import { GameSessionService } from './game-session.service';

@Module({
  imports: [AuthModule],
  controllers: [GameSessionController],
  providers: [GameSessionService],
})
export class GameSessionModule {}
