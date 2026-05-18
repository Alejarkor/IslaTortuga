import { Injectable } from '@nestjs/common';
import { pbkdf2Sync, randomBytes, timingSafeEqual } from 'node:crypto';

const HASH_ALGORITHM = 'sha512';
const ITERATIONS = 120_000;
const KEY_LENGTH = 64;
const SEPARATOR = ':';

@Injectable()
export class PasswordService {
  hashPassword(password: string): string {
    const salt = randomBytes(16).toString('hex');
    const hash = pbkdf2Sync(
      password,
      salt,
      ITERATIONS,
      KEY_LENGTH,
      HASH_ALGORITHM,
    ).toString('hex');

    return [ITERATIONS, salt, hash].join(SEPARATOR);
  }

  verifyPassword(password: string, storedPasswordHash: string): boolean {
    const [iterationsRaw, salt, storedHash] = storedPasswordHash.split(SEPARATOR);
    const iterations = Number(iterationsRaw);

    if (!iterations || !salt || !storedHash) {
      return false;
    }

    const hash = pbkdf2Sync(
      password,
      salt,
      iterations,
      KEY_LENGTH,
      HASH_ALGORITHM,
    ).toString('hex');

    const storedBuffer = Buffer.from(storedHash, 'hex');
    const hashBuffer = Buffer.from(hash, 'hex');

    if (storedBuffer.length !== hashBuffer.length) {
      return false;
    }

    return timingSafeEqual(storedBuffer, hashBuffer);
  }
}
