import {
  BadRequestException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService, type JwtSignOptions } from '@nestjs/jwt';
import { PrismaService } from '../prisma/prisma.service';
import { LoginDto } from './dto/login.dto';
import { RegisterDto } from './dto/register.dto';
import { PasswordService } from './password.service';

type AuthUser = {
  id: string;
  email: string;
  profile: {
    id: string;
    nickname: string;
    avatarId: string | null;
  } | null;
};

@Injectable()
export class AuthService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly jwtService: JwtService,
    private readonly passwordService: PasswordService,
  ) {}

  async register(dto: RegisterDto) {
    const email = dto.email?.trim().toLowerCase();
    const nickname = dto.nickname?.trim();

    if (!email || !dto.password || !nickname) {
      throw new BadRequestException('Email, password and nickname are required');
    }

    if (dto.password.length < 8) {
      throw new BadRequestException('Password must have at least 8 characters');
    }

    const existingUser = await this.prisma.user.findUnique({
      where: { email },
    });

    if (existingUser) {
      throw new BadRequestException('Email is already registered');
    }

    const existingProfile = await this.prisma.profile.findUnique({
      where: { nickname },
    });

    if (existingProfile) {
      throw new BadRequestException('Nickname is already taken');
    }

    const passwordHash = this.passwordService.hashPassword(dto.password);

    const user = await this.prisma.user.create({
      data: {
        email,
        passwordHash,
        profile: {
          create: {
            nickname,
          },
        },
      },
      include: {
        profile: true,
      },
    });

    return this.buildAuthResponse(user);
  }

  async login(dto: LoginDto) {
    const email = dto.email?.trim().toLowerCase();

    if (!email || !dto.password) {
      throw new BadRequestException('Email and password are required');
    }

    const user = await this.prisma.user.findUnique({
      where: { email },
      include: {
        profile: true,
      },
    });

    if (!user) {
      throw new UnauthorizedException('Invalid credentials');
    }

    const isPasswordValid = this.passwordService.verifyPassword(
      dto.password,
      user.passwordHash,
    );

    if (!isPasswordValid) {
      throw new UnauthorizedException('Invalid credentials');
    }

    return this.buildAuthResponse(user);
  }

  async getMe(userId: string) {
    const user = await this.prisma.user.findUnique({
      where: { id: userId },
      include: {
        profile: true,
      },
    });

    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    return this.toPublicUser(user);
  }

  async validateToken(token: string): Promise<{ sub: string; email: string }> {
    try {
      return await this.jwtService.verifyAsync(token, {
        secret: this.getJwtSecret(),
      });
    } catch {
      throw new UnauthorizedException('Invalid token');
    }
  }

  private buildAuthResponse(user: AuthUser) {
    const signOptions: JwtSignOptions = {
      secret: this.getJwtSecret(),
      expiresIn: this.getJwtExpiresIn(),
    };

    return {
      accessToken: this.jwtService.sign(
        {
          sub: user.id,
          email: user.email,
        },
        signOptions,
      ),
      user: this.toPublicUser(user),
    };
  }

  private toPublicUser(user: AuthUser) {
    return {
      id: user.id,
      email: user.email,
      profile: user.profile
        ? {
            id: user.profile.id,
            nickname: user.profile.nickname,
            avatarId: user.profile.avatarId,
          }
        : null,
    };
  }

  private getJwtSecret() {
    return process.env.JWT_SECRET ?? 'dev_secret_change_me';
  }

  private getJwtExpiresIn(): JwtSignOptions['expiresIn'] {
    return (process.env.JWT_EXPIRES_IN ?? '7d') as JwtSignOptions['expiresIn'];
  }
}
