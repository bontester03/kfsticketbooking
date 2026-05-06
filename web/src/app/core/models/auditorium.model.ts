export interface Auditorium {
  id: string;
  name: string;
  location: string;
  capacity: number;
  description?: string | null;
  isActive: boolean;
}

export interface CreateAuditoriumRequest {
  name: string;
  location: string;
  capacity: number;
  description?: string | null;
}

export interface UpdateAuditoriumRequest extends CreateAuditoriumRequest {
  isActive: boolean;
}
