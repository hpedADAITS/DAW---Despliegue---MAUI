const express = require('express');
const cors = require('cors');
const RadioBrowser = require('radio-browser');

const app = express();
const port = process.env.PORT || 3000;
const RADIO_BROWSER_HOSTS = (process.env.RADIO_BROWSER_HOSTS || 'https://de1.api.radio-browser.info,https://de2.api.radio-browser.info,https://fi1.api.radio-browser.info,https://nl1.api.radio-browser.info')
  .split(',')
  .map((h) => h.trim())
  .filter(Boolean);
const RADIO_BROWSER_AGENT = process.env.RADIO_BROWSER_AGENT || 'MauiPlayer/1.0 (api-server)';
RadioBrowser.userAgent = RADIO_BROWSER_AGENT;
RadioBrowser.service_url = RADIO_BROWSER_HOSTS[Math.floor(Math.random() * RADIO_BROWSER_HOSTS.length)];


const defaultOrigins = [
  'http://localhost',
  'http://127.0.0.1',
  'http://10.0.2.2',
  'https://localhost',
  'https://127.0.0.1'
];
const envOrigins = (process.env.CORS_ORIGINS || '')
  .split(',')
  .map((o) => o.trim())
  .filter(Boolean);
const allowedOrigins = [...defaultOrigins, ...envOrigins];
const allowAzureWildcard = (process.env.CORS_ALLOW_AZURE || 'true').toLowerCase() === 'true';

const corsOptions = {
  origin: (origin, callback) => {
    if (!origin) {
      return callback(null, true);
    }

    if (allowedOrigins.includes(origin)) {
      return callback(null, true);
    }

    if (allowAzureWildcard && /^https?:\/\/[a-z0-9.-]+\.azurewebsites\.net(:\d+)?$/i.test(origin)) {
      return callback(null, true);
    }

    return callback(new Error('Not allowed by CORS'), false);
  },
  credentials: true,
  optionsSuccessStatus: 200
};

app.use(cors(corsOptions));
app.use(express.json());


const state = {
  counter: 3,
  lastUpdated: new Date().toISOString(),
};

const greeting = {
  title: 'Welcome to Audio Media Player',
  subtitle: 'Your music streaming companion',
};

app.get('/api/status', (req, res) => {
  res.json({
    app: 'MauiApp1',
    api: 'Node Express',
    serverTime: new Date().toISOString(),
  });
});

app.get('/api/greeting', (req, res) => {
  res.json({
    ...greeting,
    serverTime: new Date().toISOString(),
  });
});

app.get('/api/counter', (req, res) => {
  res.json(state);
});

app.post('/api/counter', (req, res) => {
  const { delta = 1 } = req.body || {};
  state.counter += Number(delta) || 0;
  state.lastUpdated = new Date().toISOString();
  res.json(state);
});


const FALLBACK_RADIO_COVER = 'https://via.placeholder.com/300?text=Radio';
const MAX_RADIO_LIMIT = 1024;
const DEFAULT_RADIO_LIMIT = 50;
const MIN_RADIO_BITRATE = 192;
const RADIO_CACHE_TTL = 10 * 60 * 1000; 
const radioCache = new Map();

// Extended music genres for comprehensive radio station discovery
const EXTENDED_MUSIC_GENRES = [
  // Pop & Contemporary
  'pop', 'indie pop', 'synth-pop', 'electropop',
  // Rock
  'rock', 'alternative rock', 'indie rock', 'classic rock', 'hard rock', 'punk',
  // Electronic & Dance
  'electronic', 'house', 'techno', 'trance', 'drum and bass', 'dubstep', 'edm',
  // Hip Hop & Rap
  'hip hop', 'rap', 'trap', 'grime', 'r&b', 'soul',
  // Latin & Reggae
  'latin', 'reggae', 'reggaeton', 'salsa', 'bachata', 'cumbia', 'merengue',
  // Jazz & Blues
  'jazz', 'blues', 'funk', 'smooth jazz',
  // Classical & Acoustic
  'classical', 'acoustic', 'folk', 'singer-songwriter',
  // Other Genres
  'ambient', 'lofi', 'chill', 'instrumental', 'world music', 'experimental',
  'metal', 'country', 'gospel', 'news', 'talk', 'spoken word'
];

function extractPrimaryTag(tags) {
  if (!tags) return 'Mixed';
  if (Array.isArray(tags)) {
    const first = tags.find(tag => !!tag?.trim());
    return first ? first.trim() : 'Mixed';
  }

  const parsed = tags.split(',').map(tag => tag.trim()).filter(Boolean);
  return parsed[0] || 'Mixed';
}

function mapStationToRadio(station, prefix, index) {
  const hasCover = !!(station.favicon && station.favicon.trim().length > 0);

  return {
    stationuuid: station.stationuuid || `${prefix}_${index}`,
    checkuuid: station.checkuuid || null,
    clickuuid: station.clickuuid || null,
    title: station.name || 'Unknown Station',
    artist: (station.countrycode && station.countrycode.toUpperCase()) || 'Online Radio',
    countrycode: station.countrycode || null,
    genre: extractPrimaryTag(station.tags),
    url: station.url_resolved || station.url,
    duration: '∞',
    durationSeconds: 0,
    bitrate: Number(station.bitrate) || 128,
    format: 'stream',
    coverUrl: station.favicon || FALLBACK_RADIO_COVER,
    isRadio: true,
    clickCount: Number(station.clickcount) || 0,
    hasCover
  };
}

function dedupeStations(stations) {
  const seen = new Set();
  const unique = [];

  stations.forEach((station) => {
    const streamUrl = station.url_resolved || station.url;
    if (!streamUrl) {
      return;
    }

    const key = station.stationuuid || streamUrl;
    if (seen.has(key)) {
      return;
    }
    seen.add(key);
    unique.push({
      ...station,
      url_resolved: streamUrl,
      url: streamUrl
    });
  });

  return unique;
}

function shuffleStations(stations) {
  for (let i = stations.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [stations[i], stations[j]] = [stations[j], stations[i]];
  }
  return stations;
}

function clampLimit(limit, defaultValue = DEFAULT_RADIO_LIMIT) {
  const parsed = parseInt(limit);
  if (Number.isNaN(parsed) || parsed <= 0) {
    return defaultValue;
  }
  return Math.min(parsed, MAX_RADIO_LIMIT);
}

function filterStationsByBitrate(stations, minBitrate = MIN_RADIO_BITRATE) {
  const filtered = stations.filter((s) => {
    const bitrate = parseInt(s.bitrate) || 0;
    return bitrate >= minBitrate;
  });

  if (filtered.length === 0 && minBitrate > 192) {
    const fallback192 = stations.filter((s) => {
      const bitrate = parseInt(s.bitrate) || 0;
      return bitrate >= 192;
    });
    if (fallback192.length > 0) {
      return fallback192;
    }
  }

  if (filtered.length === 0 && minBitrate > 128) {
    const fallback128 = stations.filter((s) => {
      const bitrate = parseInt(s.bitrate) || 0;
      return bitrate >= 128;
    });
    if (fallback128.length > 0) {
      return fallback128;
    }
  }

  return filtered;
}

async function getStationsWithFallback(filter) {
  const hostsToTry = [RadioBrowser.service_url, ...RADIO_BROWSER_HOSTS.filter(h => h !== RadioBrowser.service_url), null];

  for (const host of hostsToTry) {
    try {
      RadioBrowser.service_url = host || null; 
      return await RadioBrowser.getStations(filter);
    } catch (error) {
      console.warn(`[Radio] Host failed (${host || 'random'}): ${error.message}`);
    }
  }

  throw new Error('All radio-browser hosts failed');
}

function getCachedStations(cacheKey) {
  const entry = radioCache.get(cacheKey);
  if (entry && Date.now() - entry.timestamp < RADIO_CACHE_TTL) {
    return entry.data;
  }
  return null;
}

function setCachedStations(cacheKey, data) {
  radioCache.set(cacheKey, { data, timestamp: Date.now() });
}

app.get('/api/radios/search', async (req, res) => {
  try {
    const { searchterm = 'jazz', by = 'tag' } = req.query;
    const limit = clampLimit(req.query.limit, 20);
    const fetchLimit = Math.min(MAX_RADIO_LIMIT, limit * 2);
    
    const filter = {
      limit: fetchLimit,
      by,
      searchterm,
      hidebroken: true
    };
    
    const cacheKey = `search:${by}:${searchterm}:${limit}`;
    const cached = getCachedStations(cacheKey);
    let stations;
    if (cached) {
      stations = cached;
      console.log(`[Radio] Using cached search results for ${by}="${searchterm}"`);
    } else {
      console.log(`[Radio] Searching: ${by}="${searchterm}" (limit: ${limit}, fetch: ${fetchLimit}) via ${RadioBrowser.service_url || 'random host'}`);
      const fetched = await getStationsWithFallback(filter);
      stations = filterStationsByBitrate(fetched).slice(0, limit);
      setCachedStations(cacheKey, stations);
    }
    
    const radios = stations.map((station, index) => mapStationToRadio(station, 'radio', index));
    
    res.json(radios);
    console.log(`[Radio] Returned ${radios.length} stations`);
  } catch (error) {
    console.error('Error searching radios:', error);
    res.status(500).json({ error: 'Failed to search radio stations' });
  }
});

app.get('/api/radios/topvoted', async (req, res) => {
  try {
    const limit = clampLimit(req.query.limit, 20);
    const fetchLimit = Math.min(MAX_RADIO_LIMIT, limit * 2);
    
    const filter = {
      by: 'topvote',
      limit: fetchLimit,
      hidebroken: true
    };
    
    const cacheKey = `topvoted:${limit}`;
    const cached = getCachedStations(cacheKey);
    let stations;
    if (cached) {
      stations = cached;
      console.log(`[Radio] Using cached top voted stations (limit: ${limit})`);
    } else {
      console.log(`[Radio] Fetching top voted stations (limit: ${limit}, fetch: ${fetchLimit}) via ${RadioBrowser.service_url || 'random host'}`);
      const fetched = await getStationsWithFallback(filter);
      stations = filterStationsByBitrate(fetched).slice(0, limit);
      setCachedStations(cacheKey, stations);
    }
    
    const radios = stations.map((station, index) => mapStationToRadio(station, 'radio_topvote', index));
    
    res.json(radios);
    console.log(`[Radio] Returned ${radios.length} top voted stations`);
  } catch (error) {
    console.error('Error fetching top voted radios:', error);
    res.status(500).json({ error: 'Failed to fetch top voted stations' });
  }
});

app.get('/api/radios/random', async (req, res) => {
  try {
    const limit = clampLimit(req.query.limit, 10);
    const fetchLimit = Math.min(MAX_RADIO_LIMIT, limit * 2);
    
    const cacheKey = `random:${limit}`;
    const cached = getCachedStations(cacheKey);
    let stations;
    if (cached) {
      stations = cached;
      console.log(`[Radio] Using cached random stations (limit: ${limit})`);
    } else {
      console.log(`[Radio] Fetching random stations (limit: ${limit}, fetch: ${fetchLimit}) via ${RadioBrowser.service_url || 'random host'}`);
      const fetched = await getStationsWithFallback({ 
        limit: fetchLimit,
        hidebroken: true 
      });
      stations = filterStationsByBitrate(fetched).slice(0, limit);
      setCachedStations(cacheKey, stations);
    }
    
    const radios = stations.map((station, index) => mapStationToRadio(station, 'radio_random', index));
    
    res.json(radios);
    console.log(`[Radio] Returned ${radios.length} random stations`);
  } catch (error) {
    console.error('Error fetching random radios:', error);
    res.status(500).json({ error: 'Failed to fetch random stations' });
  }
});

app.get('/api/radios/variety', async (req, res) => {
  try {
    const { tags } = req.query;
    const totalLimit = clampLimit(req.query.limit, DEFAULT_RADIO_LIMIT);

    const defaultTags = ['pop', 'rock', 'jazz', 'electronic', 'hip hop', 'latin', 'classical', 'lofi', 'news', 'talk'];
    const tagList = typeof tags === 'string' && tags.trim().length > 0
      ? tags.split(',').map(tag => tag.trim()).filter(Boolean)
      : defaultTags;

    const perTagLimit = Math.max(3, Math.ceil(totalLimit / tagList.length));
    const fetchPerTag = Math.min(MAX_RADIO_LIMIT, perTagLimit * 2);

    const cacheKey = `variety:${tagList.join('|')}:${totalLimit}`;
    const cached = getCachedStations(cacheKey);
    if (cached) {
      const radios = cached.map((station, index) => mapStationToRadio(station, 'radio_variety', index));
      res.json(radios);
      console.log(`[Radio] Returned ${radios.length} variety stations (cached)`);
      return;
    }

    console.log(`[Radio] Fetching variety mix (limit: ${totalLimit}) with tags: ${tagList.join(', ')} (per tag: ${perTagLimit}, fetch: ${fetchPerTag})`);

    const tagRequests = tagList.map(async (tag) => {
      try {
        const offset = Math.floor(Math.random() * 100);
        return await getStationsWithFallback({
          by: 'tag',
          searchterm: tag,
          limit: fetchPerTag,
          offset,
          hidebroken: true
        });
      } catch (error) {
        console.error(`[Radio] Error fetching tag "${tag}":`, error.message);
        return [];
      }
    });

    const topVotedPromise = getStationsWithFallback({
      by: 'topvote',
      limit: Math.min(10, totalLimit),
      hidebroken: true
    }).catch((error) => {
      console.error('[Radio] Error fetching top voted for variety:', error.message);
      return [];
    });

    const [tagStations, topVotedStations] = await Promise.all([
      Promise.all(tagRequests),
      topVotedPromise
    ]);

    const combinedStations = dedupeStations([...tagStations.flat(), ...topVotedStations]);
    const filteredStations = filterStationsByBitrate(combinedStations);
    const shuffledStations = shuffleStations(filteredStations);
    const limitedStations = shuffledStations.slice(0, totalLimit);
    const radios = limitedStations.map((station, index) => mapStationToRadio(station, 'radio_variety', index));

    setCachedStations(cacheKey, limitedStations);
    res.json(radios);
    console.log(`[Radio] Returned ${radios.length} variety stations (pool: ${filteredStations.length} unique)`);
  } catch (error) {
    console.error('Error fetching variety radios:', error);
    res.status(500).json({ error: 'Failed to fetch radio variety' });
  }
});

app.get('/api/radios/comprehensive', async (req, res) => {
  try {
    const limit = clampLimit(req.query.limit, 1024);
    const fetchPerGenre = Math.max(10, Math.ceil(limit / EXTENDED_MUSIC_GENRES.length));
    
    const cacheKey = `comprehensive:${limit}`;
    const cached = getCachedStations(cacheKey);
    if (cached) {
      const radios = cached.map((station, index) => mapStationToRadio(station, 'radio_comprehensive', index));
      res.json(radios);
      console.log(`[Radio] Returned ${radios.length} comprehensive stations (cached)`);
      return;
    }

    console.log(`[Radio] Fetching comprehensive mix (limit: ${limit}) - ${EXTENDED_MUSIC_GENRES.length} genres in EN/ES languages`);

    const genreRequests = EXTENDED_MUSIC_GENRES.map(async (genre) => {
      try {
        // Fetch English stations
        const enStations = await getStationsWithFallback({
          by: 'tag',
          searchterm: genre,
          limit: fetchPerGenre,
          hidebroken: true,
          language: 'en'
        }).catch(() => []);
        
        // Fetch Spanish stations
        const esStations = await getStationsWithFallback({
          by: 'tag',
          searchterm: genre,
          limit: fetchPerGenre,
          hidebroken: true,
          language: 'es'
        }).catch(() => []);
        
        return [...enStations, ...esStations];
      } catch (error) {
        console.error(`[Radio] Error fetching genre "${genre}":`, error.message);
        return [];
      }
    });

    console.log(`[Radio] Requesting stations from ${EXTENDED_MUSIC_GENRES.length} genres...`);
    const allStations = await Promise.all(genreRequests);

    const combinedStations = dedupeStations(allStations.flat());
    const filteredStations = filterStationsByBitrate(combinedStations);
    const shuffledStations = shuffleStations(filteredStations);
    const limitedStations = shuffledStations.slice(0, limit);
    const radios = limitedStations.map((station, index) => mapStationToRadio(station, 'radio_comprehensive', index));

    setCachedStations(cacheKey, limitedStations);
    res.json(radios);
    console.log(`[Radio] Returned ${radios.length} comprehensive stations (fetched: ${filteredStations.length} unique, requested: ${limit})`);
  } catch (error) {
    console.error('Error fetching comprehensive radios:', error);
    res.status(500).json({ error: 'Failed to fetch comprehensive radio stations' });
  }
});

app.listen(port, '0.0.0.0', () => {
  console.log(`API listening on http://0.0.0.0:${port}`);
  console.log(`  From host machine: http://localhost:${port}`);
  console.log(`  From Android emulator: http://10.0.2.2:${port}`);
  console.log(`  Local song endpoints disabled (radio-only API)`);
  console.log(`  Comprehensive radio: ${EXTENDED_MUSIC_GENRES.length} music genres in English/Spanish`);
});
