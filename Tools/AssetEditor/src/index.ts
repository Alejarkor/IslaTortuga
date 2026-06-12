import express from "express";
import fs from "fs";
import { config } from "./config";
import { apiRoutes } from "./routes/apiRoutes";

const app = express();

app.use(express.json({ limit: "2mb" }));

// API local de la herramienta
app.use("/api", apiRoutes);

// Frontend SPA
app.use(express.static(config.publicDir));

app.listen(config.port, () => {
  console.log("");
  console.log("  ┌─────────────────────────────────────────────┐");
  console.log("  │   IslaT AssetEditor                         │");
  console.log("  └─────────────────────────────────────────────┘");
  console.log(`   UI:          http://localhost:${config.port}`);
  console.log(`   Assets root: ${config.assetsRoot}`);
  console.log(`   GameApi:     ${config.gameApiUrl}`);
  console.log(
    `   Admin token: ${config.adminToken ? "configurado" : "NO configurado (.env)"}`
  );

  if (!fs.existsSync(config.assetsRoot)) {
    console.warn(
      `   AVISO: la carpeta de assets no existe: ${config.assetsRoot}`
    );
  }

  console.log("");
});
