import * as React from "react";
import { createRoot } from 'react-dom/client';
import { HashRouter, Routes, Route } from "react-router-dom";
import "../../styles/index.css";
import Editor from "../components/DocumentEditor/DocumentEditor";

const root = createRoot(document.getElementById("content-area") as HTMLElement);
root.render(
  <HashRouter>
    <Routes>
      <Route path="/" element={<Editor />} />
    </Routes>
  </HashRouter>
);
