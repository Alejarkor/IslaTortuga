import crypto from "crypto";
import fs from "fs";

/**
 * Calcula el hash SHA-256 de un archivo en streaming.
 * Formato de salida: "sha256-<hex>"
 */
export function hashFile(absolutePath: string): Promise<string> {
  return new Promise((resolve, reject) => {
    const hash = crypto.createHash("sha256");
    const stream = fs.createReadStream(absolutePath);

    stream.on("data", (chunk) => hash.update(chunk));
    stream.on("error", reject);
    stream.on("end", () => resolve(`sha256-${hash.digest("hex")}`));
  });
}
