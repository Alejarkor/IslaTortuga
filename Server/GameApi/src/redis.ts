import Redis from "ioredis";

/**
 * Subconjunto de comandos de Redis que usa el GameApi. Tener una interfaz propia
 * (en vez de depender del tipo completo de ioredis) permite inyectar un doble en
 * memoria en los tests sin levantar un Redis real.
 */
export interface RedisLike {
  get(key: string): Promise<string | null>;
  set(key: string, value: string, ...args: any[]): Promise<unknown>;
  del(...keys: string[]): Promise<number>;
  getdel(key: string): Promise<string | null>;
  sadd(key: string, ...members: string[]): Promise<number>;
  srem(key: string, ...members: string[]): Promise<number>;
  smembers(key: string): Promise<string[]>;
  exists(...keys: string[]): Promise<number>;
}

let client: Redis | null = null;

/**
 * Devuelve un cliente Redis compartido (singleton) construido a partir del
 * entorno. Lazy: solo conecta la primera vez que se pide.
 */
export function getRedis(): Redis {
  if (client) {
    return client;
  }

  client = new Redis({
    host: process.env.REDIS_HOST ?? "localhost",
    port: Number(process.env.REDIS_PORT ?? 6379),
    lazyConnect: false,
    maxRetriesPerRequest: 3
  });

  // Sin este manejador, ioredis emite "Unhandled error event" y un fallo de
  // conexión podría tumbar el proceso. Aquí lo registramos de forma concisa.
  client.on("error", (err: Error) => {
    console.error("[redis] error de conexión:", err.message);
  });

  return client;
}

export async function closeRedis(): Promise<void> {
  if (client) {
    await client.quit();
    client = null;
  }
}
