import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import 'primereact/resources/primereact.min.css';
import 'primeicons/primeicons.css';
import App from './App';
import './styles/app.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
