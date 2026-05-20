import { Module } from '@nestjs/common';
import { AuthModule } from './auth/auth.module';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { GameSessionModule } from './game-session/game-session.module';
import { HealthController } from './health/health.controller';
import { PrismaModule } from './prisma/prisma.module';

@Module({
  imports: [PrismaModule, AuthModule, GameSessionModule],
  controllers: [AppController, HealthController],
  providers: [AppService],
})
export class AppModule {}
