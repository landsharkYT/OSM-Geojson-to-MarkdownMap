import type { MapModel } from '../types'

// Small fictional MapModel for component tests (no real-location data).
export const rivertown: MapModel = {
  title: 'Old Town, Rivertown',
  bounds: [-120.5008, 45.5194, -120.499, 45.5212],
  features: [
    { token: '[01]', name: 'Founders Mural', category: 'landmark.artwork', importance: 95, tier: 'landmark', street: 'Main Street', district: 'Old Town', lon: -120.5001, lat: 45.521 },
    { token: '[02]', name: 'The Anchor Tavern', category: 'food.bar', importance: 65, tier: 'destination', street: 'Main Street', district: 'Old Town', lon: -120.5001, lat: 45.5203 },
  ],
  minors: [
    { name: 'Maple Apartments', category: 'residential.apartments', district: 'Old Town', lon: -120.5002, lat: 45.5206 },
  ],
  edges: [
    { fromToken: '[01]', toToken: '[02]', toName: 'The Anchor Tavern', meters: 80, dir: 'S', bucket: 'near' },
    { fromToken: '[02]', toToken: '[01]', toName: 'Founders Mural', meters: 80, dir: 'N', bucket: 'near', crosses: 'Route 9' },
  ],
  districts: [
    { name: 'Old Town', street: 'Main Street', spineDir: 'N–S', spineTokens: ['[01]', '[02]'], promotedCount: 2, clusteredCount: 1, anchorLon: -120.5, anchorLat: 45.5207 },
  ],
  terrain: [
    { name: 'Mill Lake', kind: 'water', kindLabel: 'water', note: 'open water', position: 'W', geometryType: 'Polygon', parts: [[[-120.5007, 45.5199], [-120.5004, 45.5199], [-120.5004, 45.5205], [-120.5007, 45.5205], [-120.5007, 45.5199]]] },
  ],
  markdown: '# MARKDOWNMAP — Old Town, Rivertown\n\n## Connections\n\n```\n[01] Founders Mural (landmark.artwork)\n```\n',
}
