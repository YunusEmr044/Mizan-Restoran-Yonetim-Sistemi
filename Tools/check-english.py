#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
İngilizce site denetimi: çalışan uygulamada /en sayfalarını (QR menü dahil) gezer ve görünen metinde,
öznitelik metinlerinde ve JSON-LD yapısal verisinde Türkçe harf içeren kelimeleri listeler.
Restoran/şube/alan/masa adları, adres ve müşteri yorumları kullanıcı içeriği sayılır ve atlanır.
Türkçe harf içermeyen Türkçe kelimeleri ("Masa" gibi) yakalamaz; ekran görüntüsüyle de kontrol edin.

Kullanım (uygulama http://localhost:5080'de çalışırken, Windows kimlik doğrulamalı yerel SQL Server ile):
    python Tools/check-english.py
Çıkış kodu: Türkçe kalan sayfa varsa 1.
"""
import re, sys, urllib.request, urllib.error, html, subprocess, json, os
from collections import deque, defaultdict

BASE = "http://localhost:5080"
HERE = os.path.dirname(os.path.abspath(__file__))
import tempfile

def sql_json(query):
    tmp = os.path.join(tempfile.gettempdir(), 'mizan-check-english.out')
    subprocess.run(['sqlcmd', '-S', r'YUNUS\SQLEXPRESS', '-d', 'RestoranYonetimDb', '-E', '-C', '-I', '-h', '-1', '-y', '0', '-u', '-o', tmp, '-Q', 'SET NOCOUNT ON; ' + query + ' FOR JSON PATH'], check=True)
    raw = open(tmp, 'rb').read().decode('utf-16').lstrip('﻿')
    joined = ''.join(raw.splitlines())
    return json.loads(joined) if joined.strip() else []

# Kullanıcı içeriği ve özel adlar: restoran adı, adres, yorum yapan müşterilerin adları/yorumları, masa/alan adları.
allowed_text = set()
for row in sql_json("SELECT HeroTitle AS a, ContactAddress AS b, ContactEmail AS c FROM SiteContents"):
    allowed_text.update(v for v in row.values() if v)
for row in sql_json("SELECT Name AS a, Comment AS b FROM Feedbacks WHERE IsPublished=1"):
    allowed_text.update(v for v in row.values() if v)
for row in sql_json("SELECT Name AS a FROM Branches UNION SELECT Name FROM Areas UNION SELECT Name FROM Restaurants"):
    allowed_text.update(v for v in row.values() if v)
for row in sql_json("SELECT Name AS a FROM RestaurantTables"):
    allowed_text.update(v for v in row.values() if v)
allowed_words = set()
for t in allowed_text:
    allowed_words.update(re.findall(r"[\wçğışöüÇĞİŞÖÜ'’-]+", t))
allowed_words.update({'Türkçe', 'TR'})

turkish = re.compile(r"[\wçğışöüÇĞİŞÖÜ]*[çğışöüÇĞİŞÖÜ][\wçğışöüÇĞİŞÖÜ]*")
skip = re.compile(r"(\.(png|jpe?g|webp|svg|ico|css|js|pdf|xml|txt)$)", re.I)

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None

opener = urllib.request.build_opener(NoRedirect)
start = ["/en", "/en/menu", "/en/hakkimizda", "/en/galeri", "/en/iletisim", "/en/sss", "/en/rezervasyon", "/en/kvkk", "/en/gizlilik", "/en/cerez-politikasi", "/en/kullanim-kosullari", "/en/degerlendirme"]
for row in sql_json("SELECT TOP 2 QrToken AS t FROM RestaurantTables"):
    start.append("/en/menu/" + row["t"])
seen, queue = set(), deque(start)
findings = defaultdict(set)
while queue:
    path = queue.popleft()
    if path in seen or len(seen) > 120:
        continue
    seen.add(path)
    try:
        body = opener.open(BASE + path, timeout=20).read().decode('utf-8', 'replace')
    except urllib.error.HTTPError as e:
        if e.code not in (301, 302):
            findings[path].add(f'HTTP {e.code}')
        continue
    for href in re.findall(r'href="([^"#?]+)"', body):
        href = html.unescape(href)
        if href.startswith('/en') and not skip.search(href) and href not in seen:
            queue.append(href)
    jsonld = ' '.join(re.findall(r'<script type="application/ld\+json">(.*?)</script>', body, re.S))
    text = re.sub(r'<script.*?</script>|<style.*?</style>', ' ', body, flags=re.S)
    text = html.unescape(re.sub(r'<[^>]+>', ' ', text))
    attrs = ' '.join(html.unescape(a) for a in re.findall(r'(?:alt|title|placeholder|aria-label|content)="([^"]*)"', body))
    for word in turkish.findall(text + ' ' + attrs + ' ' + jsonld):
        if word not in allowed_words:
            findings[path].add(word)
    if re.search(r'"(url|item|@id)":\s*"https?://[^"]*?/(menu|hakkimizda|galeri|iletisim|sss)"', jsonld) and not re.search(r'/en/', jsonld):
        findings[path].add('[JSON-LD /en adresi yok]')

print(f"gezilen sayfa: {len(seen)}")
bad = {p: w for p, w in findings.items() if w}
for p, words in sorted(bad.items()):
    print(p, '->', ', '.join(sorted(words))[:300])
print('TÜRKÇE KALAN SAYFA:', len(bad))
sys.exit(1 if bad else 0)
