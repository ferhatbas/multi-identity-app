import { http } from '@/services/http/httpService';

export interface MenuNode {
  id: number;
  code: string;
  title: string;
  path: string | null;
  icon: string | null;
  displayOrder: number;
  children: MenuNode[];
}

export const navigationApi = {
  mine: () => http.get<MenuNode[]>('/api/Navigation/mine'),
};
