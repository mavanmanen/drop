export interface ClipCollectionSummary {
  id: string;
  name: string;
}

export type Visibility = 'public' | 'unlisted' | 'private';

export interface ClipSummary {
  id: string;
  title: string;
  thumbnailUrl: string | null;
  durationSeconds: number | null;
  visibility: Visibility;
  createdAtUtc: string;
}

export interface AiSubClipSuggestion {
  title: string;
  description: string;
  startSeconds: number;
  endSeconds: number;
}

export interface AiClipAnalysisResult {
  tags: string[];
  subClips: AiSubClipSuggestion[];
  summary: string | null;
}

export interface Clip {
  id: string;
  title: string;
  description: string | null;
  originalFileName: string;
  fileSizeBytes: number;
  contentType: string;
  durationSeconds: number | null;
  hasThumbnail: boolean;
  videoUrl: string;
  thumbnailUrl: string | null;
  createdAtUtc: string;
  viewsCount: number;
  visibility: Visibility;
  tags?: string[];
  latestAiAnalysis?: AiClipAnalysisResult | null;
  collections: ClipCollectionSummary[];
  parentClip?: ClipSummary | null;
  childClips?: ClipSummary[];
}

export interface ClipListResponse {
  items: Clip[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Collection {
  id: string;
  name: string;
  description: string | null;
  createdAtUtc: string;
  clipCount: number;
  coverThumbnailUrl: string | null;
  visibility: Visibility;
  clips?: Clip[];
}

export interface AuthStatus {
  isAuthenticated: boolean;
  username: string | null;
}

export interface SystemStatus {
  name: string;
  version: string;
  adminUsername: string;
  ingestDirectory: string;
  clipsCount: number;
  collectionsCount: number;
  serverTimeUtc: string;
}

const API_BASE = '/api';

class ApiClient {
  private token: string | null = null;

  constructor() {
    this.token = localStorage.getItem('drop_token');
  }

  setToken(token: string | null) {
    this.token = token;
    if (token) {
      localStorage.setItem('drop_token', token);
    } else {
      localStorage.removeItem('drop_token');
    }
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...(options.headers as Record<string, string> || {}),
    };

    if (this.token && !headers['Authorization']) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }

    const response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers,
      credentials: 'include',
    });

    if (!response.ok) {
      let errMessage = `Error ${response.status}: ${response.statusText}`;
      try {
        const body = await response.json();
        if (body.error) errMessage = body.error;
      } catch {
        // ignore parse error
      }
      throw new Error(errMessage);
    }

    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  }

  // Auth
  async login(username: string, password: string): Promise<{ token: string; username: string }> {
    const res = await this.request<{ token: string; username: string }>('/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    this.setToken(res.token);
    return res;
  }

  async logout(): Promise<void> {
    await this.request('/auth/logout', { method: 'POST' });
    this.setToken(null);
  }

  async getAuthStatus(): Promise<AuthStatus> {
    try {
      return await this.request<AuthStatus>('/auth/status');
    } catch {
      return { isAuthenticated: false, username: null };
    }
  }

  // Clips
  async getClips(params?: {
    search?: string;
    collectionId?: string;
    visibility?: string;
    tag?: string;
    page?: number;
    pageSize?: number;
  }): Promise<ClipListResponse> {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.collectionId) query.set('collectionId', params.collectionId);
    if (params?.visibility) query.set('visibility', params.visibility);
    if (params?.tag) query.set('tag', params.tag);
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());

    const qs = query.toString() ? `?${query.toString()}` : '';
    return this.request<ClipListResponse>(`/clips${qs}`);
  }

  async getClip(id: string): Promise<Clip> {
    return this.request<Clip>(`/clips/${id}`);
  }

  async updateClip(id: string, data: { title: string; description?: string | null; visibility?: string; tags?: string[] }): Promise<Clip> {
    return this.request<Clip>(`/clips/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async deleteClip(id: string): Promise<void> {
    await this.request(`/clips/${id}`, { method: 'DELETE' });
  }

  async trimClip(id: string, data: {
    startSeconds: number;
    endSeconds: number;
    title?: string;
    description?: string;
    visibility?: string;
    collectionId?: string;
    tags?: string[];
  }): Promise<Clip> {
    return this.request<Clip>(`/clips/${id}/trim`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async analyzeClipAi(id: string, mode: 'tags' | 'subclips' | 'both', model?: string, context?: string): Promise<AiClipAnalysisResult> {
    return this.request<AiClipAnalysisResult>(`/clips/${id}/ai-analyze`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mode, model, context }),
    });
  }

  async getAiStatus(): Promise<{ isConfigured: boolean; availableModels?: string[] }> {
    return this.request<{ isConfigured: boolean; availableModels?: string[] }>('/clips/ai-status');
  }

  async uploadClip(
    formData: FormData,
    onProgress?: (percent: number) => void
  ): Promise<Clip> {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', `${API_BASE}/clips/upload`);
      xhr.withCredentials = true;

      if (this.token) {
        xhr.setRequestHeader('Authorization', `Bearer ${this.token}`);
      }

      if (onProgress && xhr.upload) {
        xhr.upload.onprogress = (evt) => {
          if (evt.lengthComputable) {
            const percent = Math.round((evt.loaded / evt.total) * 100);
            onProgress(percent);
          }
        };
      }

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            resolve(JSON.parse(xhr.responseText));
          } catch (e) {
            reject(new Error('Invalid JSON response'));
          }
        } else {
          try {
            const err = JSON.parse(xhr.responseText);
            reject(new Error(err.error || `Upload failed with status ${xhr.status}`));
          } catch {
            reject(new Error(`Upload failed with status ${xhr.status}`));
          }
        }
      };

      xhr.onerror = () => reject(new Error('Network error during upload'));
      xhr.send(formData);
    });
  }

  // Collections
  async getCollections(params?: { visibility?: string }): Promise<Collection[]> {
    const qs = params?.visibility ? `?visibility=${encodeURIComponent(params.visibility)}` : '';
    return this.request<Collection[]>(`/collections${qs}`);
  }

  async getCollection(id: string): Promise<Collection> {
    return this.request<Collection>(`/collections/${id}`);
  }

  async createCollection(data: { name: string; description?: string; visibility?: string }): Promise<Collection> {
    return this.request<Collection>('/collections', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async updateCollection(id: string, data: { name: string; description?: string; visibility?: string }): Promise<void> {
    await this.request(`/collections/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async deleteCollection(id: string): Promise<void> {
    await this.request(`/collections/${id}`, { method: 'DELETE' });
  }

  async addClipToCollection(collectionId: string, clipId: string): Promise<void> {
    await this.request(`/collections/${collectionId}/clips`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clipId }),
    });
  }

  async removeClipFromCollection(collectionId: string, clipId: string): Promise<void> {
    await this.request(`/collections/${collectionId}/clips/${clipId}`, {
      method: 'DELETE',
    });
  }

  async reorderCollection(collectionId: string, orderedClipIds: string[]): Promise<void> {
    await this.request(`/collections/${collectionId}/reorder`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ orderedClipIds }),
    });
  }

  // System
  async getSystemStatus(): Promise<SystemStatus> {
    return this.request<SystemStatus>('/system/status');
  }
}

export const api = new ApiClient();
