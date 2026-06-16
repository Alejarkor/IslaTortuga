import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";

import { router } from "@/app/router";
import { queryClient } from "@/app/queryClient";
import { UiSkin } from "@/skin/useUiSkin";
import "@/styles/globals.css";
import "@/styles/skin.css";

const rootElement = document.getElementById("root");

if (!rootElement) {
  throw new Error("No se encontró el elemento #root");
}

ReactDOM.createRoot(rootElement).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <UiSkin />
      <RouterProvider router={router} />
    </QueryClientProvider>
  </React.StrictMode>
);
