import { RedisLike } from "../redis";

/**
 * Doble de Redis en memoria para tests. Implementa solo los comandos que usa el
 * GameApi. Node es monohilo, así que operaciones como getdel son atómicas por
 * construcción (no hay interleaving real entre el get y el del).
 */
export class InMemoryRedis implements RedisLike {
  private strings = new Map<string, string>();
  private sets = new Map<string, Set<string>>();
  private expiry = new Map<string, number>(); // key -> epoch ms

  private isExpired(key: string): boolean {
    const exp = this.expiry.get(key);
    if (exp === undefined) {
      return false;
    }
    if (Date.now() >= exp) {
      this.strings.delete(key);
      this.sets.delete(key);
      this.expiry.delete(key);
      return true;
    }
    return false;
  }

  async get(key: string): Promise<string | null> {
    if (this.isExpired(key)) {
      return null;
    }
    return this.strings.has(key) ? this.strings.get(key)! : null;
  }

  async set(key: string, value: string, ...args: any[]): Promise<unknown> {
    this.strings.set(key, value);
    this.expiry.delete(key);

    // Soporta set(key, value, "EX", seconds) y set(key, value, "PX", ms)
    for (let i = 0; i < args.length - 1; i++) {
      const flag = String(args[i]).toUpperCase();
      if (flag === "EX") {
        this.expiry.set(key, Date.now() + Number(args[i + 1]) * 1000);
      } else if (flag === "PX") {
        this.expiry.set(key, Date.now() + Number(args[i + 1]));
      }
    }
    return "OK";
  }

  async del(...keys: string[]): Promise<number> {
    let count = 0;
    for (const key of keys) {
      const existed = this.strings.delete(key) || this.sets.delete(key);
      this.expiry.delete(key);
      if (existed) {
        count++;
      }
    }
    return count;
  }

  async getdel(key: string): Promise<string | null> {
    const value = await this.get(key);
    if (value !== null) {
      this.strings.delete(key);
      this.expiry.delete(key);
    }
    return value;
  }

  async sadd(key: string, ...members: string[]): Promise<number> {
    let set = this.sets.get(key);
    if (!set) {
      set = new Set<string>();
      this.sets.set(key, set);
    }
    let added = 0;
    for (const m of members) {
      if (!set.has(m)) {
        set.add(m);
        added++;
      }
    }
    return added;
  }

  async srem(key: string, ...members: string[]): Promise<number> {
    const set = this.sets.get(key);
    if (!set) {
      return 0;
    }
    let removed = 0;
    for (const m of members) {
      if (set.delete(m)) {
        removed++;
      }
    }
    return removed;
  }

  async smembers(key: string): Promise<string[]> {
    if (this.isExpired(key)) {
      return [];
    }
    const set = this.sets.get(key);
    return set ? Array.from(set) : [];
  }

  async exists(...keys: string[]): Promise<number> {
    let count = 0;
    for (const key of keys) {
      if (this.isExpired(key)) {
        continue;
      }
      if (this.strings.has(key) || this.sets.has(key)) {
        count++;
      }
    }
    return count;
  }

  /** Solo para tests: segundos de TTL restantes (o null si no tiene expiración). */
  ttlSeconds(key: string): number | null {
    const exp = this.expiry.get(key);
    if (exp === undefined) {
      return null;
    }
    return Math.max(0, Math.round((exp - Date.now()) / 1000));
  }
}
