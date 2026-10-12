from django.contrib.gis import admin
from .models import Negocio


@admin.register(Negocio)
class NegocioAdmin(admin.GISModelAdmin):
    list_display = ("nombre", "ciudad", "telefono", "activo")
    search_fields = ("nombre", "ciudad")