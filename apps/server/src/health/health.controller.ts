import { Controller, Get } from '@nestjs/common';
import { PrismaService } from '../prisma/prisma.service';

@Controller('health')
export class HealthController {
  constructor(private readonly prisma: PrismaService) {}

  @Get()
  getApiHealth() {
    return {
      status: 'ok',
      service: 'api',
      timestamp: new Date().toISOString(),
    };
  }

  @Get('db')
  async getDatabaseHealth() {
    const result = await this.prisma.$queryRaw<{ now: Date }[]>`
      SELECT NOW() as now
    `;

    return {
      status: 'ok',
      service: 'database',
      database: 'postgresql',
      timestamp: new Date().toISOString(),
      databaseTime: result[0]?.now,
    };
  }
}
